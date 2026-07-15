using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface ICustomerService
{
    // Both take applicationUserId (the JWT's subject), NOT a Customer.Id

    // Returns null when this login has no Customer profile row (e.g. an Owner/Driver token, or a
    // Customer whose profile was removed). The controller turns that into a 404, not an empty object.
    Task<CustomerProfileDto?> GetProfileAsync(int applicationUserId);

    // Updates Name/Phone only. Same null = no-profile contract as above.
    Task<CustomerProfileDto?> UpdateProfileAsync(int applicationUserId, CustomerProfileUpdateDto dto);
}
