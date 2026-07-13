namespace Takeaway_OS.API.DTOs;

public class OpeningHoursDto  // shape returned by GET /api/openinghours
{
    public int Id { get; set; }

    // Serialized as "Monday" not 1 -> Program.cs registers a JsonStringEnumConverter globally,
    // so every enum crossing the API boundary goes out as its name.
    public DayOfWeek DayOfWeek { get; set; }

    // TimeOnly serializes to "17:00:00", which the frontend can render directly.
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
}

// shape returned by GET /api/openinghours/status 
// The same object is what OrderService checks before accepting an order
// so the banner and the server-side rejection can never disagree
public class RestaurantStatusDto
{
    public bool IsOpen { get; set; }
    public string Message { get; set; } = string.Empty;

}

// shape accepted by POST /api/openinghours (Owner)
// No Id -> the database assigns it, same reason OrderCreateDto has no Id/CreatedAt.
public class OpeningHoursCreateDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }

    // CloseTime EQUAL to OpenTime is rejected by the service
    // can be zero-length window or a full 24 hours, guessing either way would be wrong.
    public TimeOnly CloseTime { get; set; }
}

// shape accepted by PUT /api/openinghours/{id} (Owner)
// Identical fields to Create (same as CategoryDto.cs) 
// sharing one DTO would silently couple the request shapes of two different endpoints together
public class OpeningHoursUpdateDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
}

// shape accepted by PUT /api/openinghours/closure (Owner) -> the holiday override.
// Deliberately separate endpoint, shouldn't affect schedule
public class ClosureUpdateDto
{
    public bool IsTemporarilyClosed { get; set; }   
    public string ClosureReason { get; set; } = string.Empty; // Ignored when IsTemporarilyClosed is false; the service clears it on reopen.
}
