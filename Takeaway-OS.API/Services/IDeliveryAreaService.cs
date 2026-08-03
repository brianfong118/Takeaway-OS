using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

// CreateAsync's outcome. An enum rather than a bool because the two failures need different
// status codes: a malformed district is the Owner's typo (400), a district already in the
// list is a collision with existing state (409). Same split as DeleteResult.
public enum DeliveryAreaCreateOutcome
{
    Success,
    InvalidFormat,  // 400: not a well-formed outward code once normalised ("E1 6AN", "hello")
    Duplicate       // 409: this district is already on the list
}

public class DeliveryAreaCreateResult
{
    public DeliveryAreaCreateOutcome Outcome { get; set; }
    public DeliveryAreaDto? Area { get; set; } // set only when Outcome is Success
}

public interface IDeliveryAreaService
{
    Task<List<DeliveryAreaDto>> GetAllAsync();

    // Normalises the submitted code before validating and storing it, which is why the format
    // check lives here and not in a [RegularExpression] on the DTO - see DeliveryAreaCreateDto.
    Task<DeliveryAreaCreateResult> CreateAsync(DeliveryAreaCreateDto dto);

    // bool, not DeleteResult: nothing has a foreign key pointing at DeliveryArea, so there is
    // no HasDependents case to report. Orders snapshot their postcode as text rather than
    // referencing a district row, deliberately - removing a district the shop no longer
    // covers must not be blocked by, or alter, the orders already delivered there.
    Task<bool> DeleteAsync(int id);

    // Takes an ALREADY-PARSED outward code ("E1"), not a full postcode. The caller parses,
    // because the caller (OrderService) needs the formatted postcode for its snapshot anyway,
    // and splitting it here would mean parsing the same string twice. 
    Task<bool> IsOutwardCodeAllowedAsync(string outwardCode);
}
