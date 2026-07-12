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
        var hours = await _context.OpeningHours
            .OrderBy(oh => oh.DayOfWeek)  // Sunday(0) -> Saturday(6); the string column stores the name, but EF orders by the underlying enum
            .ThenBy(oh => oh.OpenTime)    // so a split shift lists the lunch window before the evening one
            .ToListAsync();

        return hours.Select(oh => new OpeningHoursDto
        {
            Id = oh.Id,
            DayOfWeek = oh.DayOfWeek,
            OpenTime = oh.OpenTime,
            CloseTime = oh.CloseTime
        }).ToList();
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
}
