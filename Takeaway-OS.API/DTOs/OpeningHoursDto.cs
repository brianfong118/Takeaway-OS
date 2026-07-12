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
