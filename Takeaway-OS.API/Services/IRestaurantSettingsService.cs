using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

// Owner-configurable settings that aren't opening hours. Separate from IBusinessHoursService
// even though both read the same RestaurantSettings row: the closure flag is an
// hours concern, a delivery fee isn't
public interface IRestaurantSettingsService
{
    Task<RestaurantSettingsDto> GetAsync();
    // Two callers, deliberately one method (same reasoning as GetStatusAsync): the public
    // GET /api/settings the checkout reads the fee from, and OrderService.CreateAsync which
    // snapshots it. Sharing it is what stops the quoted fee differing from the charged one.

    Task UpdateAsync(RestaurantSettingsUpdateDto dto);
    // No result type: the row is seeded by the migration and is the only one that will ever
    // exist, so this can't 404 and can't conflict. Range validation already ran at the boundary.
}
