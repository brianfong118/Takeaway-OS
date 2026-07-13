using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class BusinessHoursService : IBusinessHoursService
{
    private readonly AppDbContext _context;
    private readonly TimeZoneInfo _timeZone;

    // IConfiguration -> where Program.cs reads JWT_SECRET from 
    // merges appsettings.json, user-secrets and environment variables into one lookup.
    public BusinessHoursService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        var timeZoneId = configuration["RESTAURANT_TIMEZONE"] ?? "Europe/London"; // fallback for dev/test if the env var isn't set
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); 
    }

    public async Task<List<OpeningHoursDto>> GetScheduleAsync()
    {
        // ToListAsync FIRST, then sort in memory. Sorting in the query would push ORDER BY into
        // SQL, and because HasConversion<string>() stores DayOfWeek as text, Postgres would sort
        // it ALPHABETICALLY - Friday, Monday, Saturday, Sunday, Thursday... - not Sunday->Saturday.
        // In memory the values are real DayOfWeek enums again, so this sorts by the enum's number.
        var hours = await _context.OpeningHours.ToListAsync();

        return hours
            .OrderBy(oh => oh.DayOfWeek)  // Sunday(0) -> Saturday(6)
            .ThenBy(oh => oh.OpenTime)    // so a split shift lists the lunch window before the evening one
            .Select(MapToDto)
            .ToList();
    }

    public async Task<RestaurantStatusDto> GetStatusAsync()
    {
        // The manual override checked first 
        var settings = await _context.RestaurantSettings.FindAsync(RestaurantSettings.SingletonId);
        if (settings is not null && settings.IsTemporarilyClosed)
        {
            return new RestaurantStatusDto
            {
                IsOpen = false,
                Message = string.IsNullOrWhiteSpace(settings.ClosureReason)
                    ? "We're temporarily closed."      // Owner closed w/o giving a reason
                    : settings.ClosureReason
            };
        }

        var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, _timeZone);
        var timeNow = TimeOnly.FromDateTime(now);
        var today = now.DayOfWeek;

        // Yesterday matters because of windows that run past midnight 
        // Past 00:00, both day's rows get pulled and IsWithin decides which side of midnight each one covers.
        var yesterday = today == DayOfWeek.Sunday ? DayOfWeek.Saturday : today - 1;

        var windows = await _context.OpeningHours
            .Where(oh => oh.DayOfWeek == today || oh.DayOfWeek == yesterday)
            .ToListAsync();

        // Any() not All(): a split-shift day has several rows and being inside ANY one means open.
        var isOpen = windows.Any(w => IsWithin(w, timeNow, isToday: w.DayOfWeek == today));

        return new RestaurantStatusDto
        {
            IsOpen = isOpen,
            Message = isOpen ? string.Empty : "We're closed right now. Please order during opening hours."
        };
    }

    // isToday distinguishes "this row is for the current weekday" from "this row is yesterday's,
    // and we're only still inside it because it runs past midnight".
    private static bool IsWithin(OpeningHours window, TimeOnly now, bool isToday)
    {
        // CloseTime <= OpenTime is how a past-midnight window encodes itself
        var spansMidnight = window.CloseTime <= window.OpenTime;

        if (!spansMidnight)
        {
            return isToday && now >= window.OpenTime && now < window.CloseTime;
        }

        // Past-midnight window, evening half: from OpenTime until midnight, on its own weekday.
        if (isToday) return now >= window.OpenTime;

        // Past-midnight window, early-hours half: yesterday's row is still running until CloseTime.
        return now < window.CloseTime;
    }

    public async Task<OpeningHoursSaveResult> CreateWindowAsync(OpeningHoursCreateDto dto)
    {
        var error = await ValidateWindowAsync(dto.DayOfWeek, dto.OpenTime, dto.CloseTime, excludeId: null);
        if (error is not null) return new OpeningHoursSaveResult { Error = error };

        var window = new OpeningHours
        {
            DayOfWeek = dto.DayOfWeek,
            OpenTime = dto.OpenTime,
            CloseTime = dto.CloseTime
        };

        _context.OpeningHours.Add(window);
        await _context.SaveChangesAsync();

        return new OpeningHoursSaveResult { Window = MapToDto(window) };
    }

    public async Task<OpeningHoursSaveResult> UpdateWindowAsync(int id, OpeningHoursUpdateDto dto)
    {
        var window = await _context.OpeningHours.FindAsync(id);
        if (window is null) return new OpeningHoursSaveResult { NotFound = true };

        // excludeId: the row being edited must not be compared against its own old values,
        // or every update would "overlap itself" and be rejected.
        var error = await ValidateWindowAsync(dto.DayOfWeek, dto.OpenTime, dto.CloseTime, excludeId: id);
        if (error is not null) return new OpeningHoursSaveResult { Error = error };

        window.DayOfWeek = dto.DayOfWeek;
        window.OpenTime = dto.OpenTime;
        window.CloseTime = dto.CloseTime;
        await _context.SaveChangesAsync();

        return new OpeningHoursSaveResult { Window = MapToDto(window) };
    }

    public async Task<bool> DeleteWindowAsync(int id)
    {
        var window = await _context.OpeningHours.FindAsync(id);
        if (window is null) return false;

        _context.OpeningHours.Remove(window);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task SetClosureAsync(ClosureUpdateDto dto)
    {
        // FindAsync, not Add: the row is seeded by the AddBusinessHours migration and there is only 1
        var settings = await _context.RestaurantSettings.FindAsync(RestaurantSettings.SingletonId);
        if (settings is null) // shouldn't happen, but if the migration was never run, create the row now rather than throwing a null-ref exception.
        {
            settings = new RestaurantSettings { Id = RestaurantSettings.SingletonId };
            _context.RestaurantSettings.Add(settings);
        }

        settings.IsTemporarilyClosed = dto.IsTemporarilyClosed;         
        settings.ClosureReason = dto.IsTemporarilyClosed ? dto.ClosureReason : string.Empty; // Reopening clears the reason 

        await _context.SaveChangesAsync();
    }

    // Returns null when the window is valid, or the reason it isn't.
    private async Task<string?> ValidateWindowAsync(DayOfWeek day, TimeOnly open, TimeOnly close, int? excludeId)
    {
        if (open == close)
            return "OpenTime and CloseTime cannot be the same. For a window that runs past midnight, set CloseTime earlier than OpenTime (e.g. 17:00 -> 00:30).";

        var candidate = ToWeekInterval(day, open, close);

        var existing = await _context.OpeningHours
            .Where(oh => excludeId == null || oh.Id != excludeId) 
            .ToListAsync();

        foreach (var other in existing)
        {
            if (Overlaps(candidate, ToWeekInterval(other.DayOfWeek, other.OpenTime, other.CloseTime))) 
                return $"This window overlaps the existing {other.DayOfWeek} {other.OpenTime:HH\\:mm}-{other.CloseTime:HH\\:mm} window.";
        }

        return null;
    }

    private const int MinutesPerDay = 1440;
    private const int MinutesPerWeek = 7 * MinutesPerDay;

    // Overlap can't be checked day-by-day, because a past-midnight window physically sits on 2 days
    // So each window converted into 1 continuous "minutes since Sunday 00:00" timeline
    private static (int Start, int Length) ToWeekInterval(DayOfWeek day, TimeOnly open, TimeOnly close)
    {
        var openMinutes = (int)open.ToTimeSpan().TotalMinutes;
        var closeMinutes = (int)close.ToTimeSpan().TotalMinutes;

        var length = closeMinutes - openMinutes;
        if (length < 0) length += MinutesPerDay; // past-midnight: close is on the following day

        return (Start: (int)day * MinutesPerDay + openMinutes, Length: length);
    }

    // The timeline is a circle, not a line: Comparing raw start/end numbers would miss that entirely.
    //
    // Measure everything RELATIVE to interval a's start.
    // Slide a to position 0, so it occupies [0, a.Length). Then b starts at some offset that is,
    // by construction, in 0..10080 - and b overlaps a in exactly two ways:
    private static bool Overlaps((int Start, int Length) a, (int Start, int Length) b)
    {
        // Modulo keeps the offset positive even when b starts "before" a in raw clock terms.
        var offset = ((b.Start - a.Start) % MinutesPerWeek + MinutesPerWeek) % MinutesPerWeek;

        // 1. b starts inside a.
        if (offset < a.Length) return true;

        // 2. b starts after a, but is long enough to wrap past the end of the week and back
        //    into a's opening minutes.
        return offset + b.Length > MinutesPerWeek;
    }

    private static OpeningHoursDto MapToDto(OpeningHours window) => new()
    {
        Id = window.Id,
        DayOfWeek = window.DayOfWeek,
        OpenTime = window.OpenTime,
        CloseTime = window.CloseTime
    };
}
