using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IBusinessHoursService
{
    Task<List<OpeningHoursDto>> GetScheduleAsync(); // returns the full schedule

    Task<RestaurantStatusDto> GetStatusAsync();
    // Two callers, deliberately one method: the public status endpoint (banner) and
    // OrderService.CreateAsync (the actual rejection). Sharing it is what guarantees the
    // banner can't say "open" while the server rejects the order.
}
