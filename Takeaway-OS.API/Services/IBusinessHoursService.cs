using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public class OpeningHoursSaveResult
{
    public OpeningHoursDto? Window { get; set; }  // null = the write was refused, see Error
    public string? Error { get; set; }            // set only when Window is null
    public bool NotFound { get; set; }            // 404, not 400: the id doesn't exist
}

public interface IBusinessHoursService
{
    Task<List<OpeningHoursDto>> GetScheduleAsync(); // returns the full schedule

    Task<RestaurantStatusDto> GetStatusAsync();
    // Two callers, deliberately one method: the public status endpoint (banner) and
    // OrderService.CreateAsync (the actual rejection). Sharing it is what guarantees the
    // banner can't say "open" while the server rejects the order.

    Task<OpeningHoursSaveResult> CreateWindowAsync(OpeningHoursCreateDto dto);
    Task<OpeningHoursSaveResult> UpdateWindowAsync(int id, OpeningHoursUpdateDto dto);

    Task<bool> DeleteWindowAsync(int id);
    // Plain bool, not DeleteResult: nothing has a foreign key to OpeningHours, so unlike
    // Category/ModifierGroup there's no "blocked by dependents" case to distinguish.
    // Deleting every window for a day is legal and => "closed that day".

    Task SetClosureAsync(ClosureUpdateDto dto);
    // No result type: the RestaurantSettings row is seeded by the migration and is the only
    // row that will ever exist, so this can't 404 and can't conflict. It always succeeds.
}
