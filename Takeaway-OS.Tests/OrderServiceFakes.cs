using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Minimal stand-ins for the services OrderService.CreateAsync consults before it builds an order,
// so creation can be tested against an in-memory database without real opening hours or settings.
// Only the methods CreateAsync calls do anything; the rest throw, so a test that unexpectedly
// reaches one fails loudly instead of passing on a made-up answer.

// The shop is always open, so tests exercise the order rules rather than the clock.
internal sealed class AlwaysOpenBusinessHours : IBusinessHoursService
{
    public Task<RestaurantStatusDto> GetStatusAsync() =>
        Task.FromResult(new RestaurantStatusDto { IsOpen = true });

    public Task<List<OpeningHoursDto>> GetScheduleAsync() => throw new NotSupportedException();
    public Task<OpeningHoursSaveResult> CreateWindowAsync(OpeningHoursCreateDto dto) => throw new NotSupportedException();
    public Task<OpeningHoursSaveResult> UpdateWindowAsync(int id, OpeningHoursUpdateDto dto) => throw new NotSupportedException();
    public Task<bool> DeleteWindowAsync(int id) => throw new NotSupportedException();
    public Task SetClosureAsync(ClosureUpdateDto dto) => throw new NotSupportedException();
}

// A fixed delivery fee, as if the Owner had set it in the settings card.
internal sealed class FixedSettingsService : IRestaurantSettingsService
{
    private readonly decimal _deliveryFee;

    public FixedSettingsService(decimal deliveryFee = 0m) => _deliveryFee = deliveryFee;

    public Task<RestaurantSettingsDto> GetAsync() =>
        Task.FromResult(new RestaurantSettingsDto { DeliveryFee = _deliveryFee });

    public Task UpdateAsync(RestaurantSettingsUpdateDto dto) => throw new NotSupportedException();
}
