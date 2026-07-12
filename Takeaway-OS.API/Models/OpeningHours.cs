namespace Takeaway_OS.API.Models;

// One row = one continuous serving window on one weekday
// Split shift = two rows for the same day (e.g. Mon 12:00-14:00 and Mon 17:00-23:00),
// NO single Open/Close pair per day.
public class OpeningHours
{
    public int Id { get; set; }

    // System.DayOfWeek -> the built-in .NET enum (Sunday = 0 ... Saturday = 6)
    // comparing [day in the restaurant's timezone] against this row is a direct == check.
    public DayOfWeek DayOfWeek { get; set; }

    // TimeOnly = a clock time with no date attached (17:30)
    // Using DateTime here would force a meaningless date onto every row.
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
}
