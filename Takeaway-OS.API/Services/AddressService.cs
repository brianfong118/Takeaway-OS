using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class AddressService : IAddressService
{
    private readonly AppDbContext _context;

    public AddressService(AppDbContext context)
    {
        _context = context;
    }

    // The invariant this whole service defends: a customer with at least one address has EXACTLY one default.
    // IsDefault is a plain bool on each row, so nothing in the database enforces this 
    // it holds only because every write below goes through these rules:
    //   - the first address a customer saves is forced to default, whatever the request said
    //   - promoting one address to default demotes whichever one held it
    //   - the default can't be demoted directly; you promote a different one instead
    //   - deleting the default promotes the oldest survivor

    private async Task<int?> ResolveCustomerIdAsync(int applicationUserId)
    {
        return await _context.Customers
            .Where(c => c.ApplicationUserId == applicationUserId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AddressDto>> GetForCustomerAsync(int applicationUserId)
    {
        var customerId = await ResolveCustomerIdAsync(applicationUserId);
        if (customerId is null) return new List<AddressDto>(); 

        var addresses = await _context.Addresses
            .Where(a => a.CustomerId == customerId) // the scoping - a customer only ever sees their own
            .OrderByDescending(a => a.IsDefault)    // default first, so the UI can just take the head of the list
            .ThenBy(a => a.Id)
            .ToListAsync();

        return addresses.Select(MapToDto).ToList();
    }

    public async Task<AddressDto?> CreateAsync(AddressCreateDto dto, int applicationUserId)
    {
        var customerId = await ResolveCustomerIdAsync(applicationUserId);
        if (customerId is null) return null;

        var isFirstAddress = !await _context.Addresses.AnyAsync(a => a.CustomerId == customerId);

        // Forced true on the first address even if the request said false 
        var shouldBeDefault = isFirstAddress || dto.IsDefault;

        if (shouldBeDefault) await ClearDefaultAsync(customerId.Value);

        var address = new Address
        {
            Label = dto.Label,
            Line1 = dto.Line1,
            Line2 = dto.Line2,
            City = dto.City,
            Postcode = dto.Postcode,
            IsDefault = shouldBeDefault,
            CustomerId = customerId.Value // from the JWT, never from the request body
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(); // one call - the demotion above and this insert commit together

        return MapToDto(address);
    }

    public async Task<AddressDto?> UpdateAsync(int id, AddressUpdateDto dto, int applicationUserId)
    {
        var customerId = await ResolveCustomerIdAsync(applicationUserId);
        if (customerId is null) return null;

        // Ownership is in the WHERE, not an if-check afterwards: someone else's address is
        // indistinguishable from one that doesn't exist, so the controller's 404 leaks nothing.
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId);

        if (address is null) return null;

        address.Label = dto.Label;
        address.Line1 = dto.Line1;
        address.Line2 = dto.Line2;
        address.City = dto.City;
        address.Postcode = dto.Postcode;

        // Promotion only. Sending IsDefault=false at the current default is ignored 
        if (dto.IsDefault && !address.IsDefault)
        {
            await ClearDefaultAsync(customerId.Value);
            address.IsDefault = true;
        }

        await _context.SaveChangesAsync();
        return MapToDto(address);
    }

    public async Task<bool> DeleteAsync(int id, int applicationUserId)
    {
        var customerId = await ResolveCustomerIdAsync(applicationUserId);
        if (customerId is null) return false;

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId);

        if (address is null) return false;

        var wasDefault = address.IsDefault;
        _context.Addresses.Remove(address);

        // Deleting the default -> promote the oldest survivor (lowest Id) so the invariant holds.
        if (wasDefault)
        {
            var survivor = await _context.Addresses
                .Where(a => a.CustomerId == customerId && a.Id != id)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync();

            if (survivor is not null) survivor.IsDefault = true; // null = that was their last address, so no default is correct
        }

        await _context.SaveChangesAsync(); // delete + promotion commit together, so there's no window with zero defaults
        return true;
    }

    // Demotes whatever currently holds the default slot. Doesn't save 
    // Caller commits it in the same SaveChangesAsync as the promotion, so the two can't half-apply.
    private async Task ClearDefaultAsync(int customerId)
    {
        var currentDefaults = await _context.Addresses
            .Where(a => a.CustomerId == customerId && a.IsDefault)
            .ToListAsync();

        foreach (var existing in currentDefaults) existing.IsDefault = false;
    }

    private static AddressDto MapToDto(Address address)
    {
        return new AddressDto
        {
            Id = address.Id,
            Label = address.Label,
            Line1 = address.Line1,
            Line2 = address.Line2,
            City = address.City,
            Postcode = address.Postcode,
            IsDefault = address.IsDefault
        };
    }
}
