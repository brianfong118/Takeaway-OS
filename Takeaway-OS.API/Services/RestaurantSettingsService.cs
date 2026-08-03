using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class RestaurantSettingsService : IRestaurantSettingsService
{
    private readonly AppDbContext _context;

    public RestaurantSettingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RestaurantSettingsDto> GetAsync()
    {
        var settings = await _context.RestaurantSettings.FindAsync(RestaurantSettings.SingletonId);

        // Falling back to a default object rather than throwing: a missing row means the
        // migration never ran, and 0 is the same value that migration would have seeded.
        return new RestaurantSettingsDto
        {
            DeliveryFee = settings?.DeliveryFee ?? 0m
        };
    }

    public async Task UpdateAsync(RestaurantSettingsUpdateDto dto)
    {
        // FindAsync, not Add: the row is seeded by the migration and there is only ever one.
        var settings = await _context.RestaurantSettings.FindAsync(RestaurantSettings.SingletonId);
        if (settings is null) 
        {
            settings = new RestaurantSettings { Id = RestaurantSettings.SingletonId };
            _context.RestaurantSettings.Add(settings);
        }

        settings.DeliveryFee = dto.DeliveryFee;
        await _context.SaveChangesAsync();
    }
}
