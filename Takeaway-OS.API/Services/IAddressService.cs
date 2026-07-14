using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

// Every method takes an applicationUserId (what the JWT carries) rather than a Customer.Id
//
// Every method is scoped to the caller, "not found" and "belongs to someone else" collapse into
// the same null/false result on purpose: the controller's 404 must not reveal that an id exists.
public interface IAddressService
{
    Task<List<AddressDto>> GetForCustomerAsync(int applicationUserId);

    // null = this login has no Customer profile (an orphaned login), so there's no address book to write to.
    Task<AddressDto?> CreateAsync(AddressCreateDto dto, int applicationUserId);

    // null = no such address, or it isn't theirs.
    Task<AddressDto?> UpdateAsync(int id, AddressUpdateDto dto, int applicationUserId);

    // false = no such address, or it isn't theirs.
    // No DeleteResult enum here (unlike Category/ModifierGroup): nothing has a Restrict FK onto Addresses, 
    // because Orders snapshot the delivery address as plain text rather than pointing at this row
    // A saved address can always be deleted, and past orders keep the address they were actually sent to. 
    // Same price-snapshotting principle as OrderItems.
    Task<bool> DeleteAsync(int id, int applicationUserId);
}
