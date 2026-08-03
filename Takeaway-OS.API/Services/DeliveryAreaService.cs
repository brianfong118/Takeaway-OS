using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class DeliveryAreaService : IDeliveryAreaService
{
    private readonly AppDbContext _context;

    public DeliveryAreaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeliveryAreaDto>> GetAllAsync()
    {
        return await _context.DeliveryAreas
            // Alphabetical, so the Owner's card and the customer's "we deliver to..." list
            // both read in a stable order
            .OrderBy(d => d.OutwardCode)
            .Select(d => new DeliveryAreaDto { Id = d.Id, OutwardCode = d.OutwardCode })
            .ToListAsync();
    }

    public async Task<DeliveryAreaCreateResult> CreateAsync(DeliveryAreaCreateDto dto)
    {
        // Normalise FIRST, then validate the normalised form. 
        var outwardCode = UkPostcode.Normalise(dto.OutwardCode);

        if (!UkPostcode.IsValidOutwardCode(outwardCode))
            return new DeliveryAreaCreateResult { Outcome = DeliveryAreaCreateOutcome.InvalidFormat };

        var exists = await _context.DeliveryAreas.AnyAsync(d => d.OutwardCode == outwardCode);
        if (exists)
            return new DeliveryAreaCreateResult { Outcome = DeliveryAreaCreateOutcome.Duplicate };

        var area = new DeliveryArea { OutwardCode = outwardCode };
        _context.DeliveryAreas.Add(area);
        await _context.SaveChangesAsync();

        return new DeliveryAreaCreateResult
        {
            Outcome = DeliveryAreaCreateOutcome.Success,
            // Id is only populated after SaveChangesAsync - the database assigns it.
            Area = new DeliveryAreaDto { Id = area.Id, OutwardCode = area.OutwardCode }
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var area = await _context.DeliveryAreas.FindAsync(id);
        if (area is null) return false;

        // A hard delete, unlike MenuItem's disable-is-a-soft-delete rule. 
        _context.DeliveryAreas.Remove(area);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsOutwardCodeAllowedAsync(string outwardCode)
    {
        // Normalised again rather than trusted: this is the check that decides whether an
        // order is refused, so it shouldn't depend on every caller having remembered to
        // normalise. Cheap, and idempotent on already-normalised input.
        var normalised = UkPostcode.Normalise(outwardCode);

        // Exact equality, NOT StartsWith. The whole point of storing outward codes: prefix
        // matching would make the district "E1" silently also allow E14, E15 and E17
        return await _context.DeliveryAreas.AnyAsync(d => d.OutwardCode == normalised);
    }
}
