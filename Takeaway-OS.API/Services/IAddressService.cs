using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public enum AddressWriteOutcome
{
    Success,
    InvalidPostcode,   // 400: UkPostcode.TryParse rejected it once normalised ("hello", "E1")
    NoCustomerProfile, // 404: a Customer-role login whose Customer row is gone (create only)
    NotFound           // 404: no such address, or it belongs to someone else (update only)
}

public class AddressWriteResult
{
    public AddressWriteOutcome Outcome { get; set; }
    public AddressDto? Address { get; set; } // set only when Outcome is Success
}

// Every method takes an applicationUserId (what the JWT carries) rather than a Customer.Id
//
// Every method is scoped to the caller, "not found" and "belongs to someone else" collapse into
// the same result on purpose: the controller's 404 must not reveal that an id exists.
public interface IAddressService
{
    Task<List<AddressDto>> GetForCustomerAsync(int applicationUserId);
    // The postcode is normalised before being stored, so the
    // format check has to live HERE rather than in a [RegularExpression] on AddressCreateDto
    // What is NOT checked here is whether the postcode is one we deliver to. That stays a single
    // gate in OrderService, checked at order time against the districts as they are THEN. 
    Task<AddressWriteResult> CreateAsync(AddressCreateDto dto, int applicationUserId);

    // An orphaned login collapses into NotFound rather than getting NoCustomerProfile of its
    // own: with no profile there are no addresses either, so "no such address" is already true.
    Task<AddressWriteResult> UpdateAsync(int id, AddressUpdateDto dto, int applicationUserId);

    // false = no such address, or it isn't theirs.
    // No DeleteResult enum here (unlike Category/ModifierGroup): nothing has a Restrict FK onto Addresses, 
    // because Orders snapshot the delivery address as plain text rather than pointing at this row
    // A saved address can always be deleted, and past orders keep the address they were actually sent to. 
    // Same price-snapshotting principle as OrderItems.
    Task<bool> DeleteAsync(int id, int applicationUserId);
}
