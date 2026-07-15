using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Extensions;  // User.GetApplicationUserId()
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

// The profile is always the CALLER'S own — the only route is "me", never /api/customers/{id}
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Customer)]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<CustomerProfileDto>> GetMe()
    {
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized(); // valid Customer role but no usable subject claim = malformed token

        var profile = await _customerService.GetProfileAsync(applicationUserId.Value);
        if (profile is null) return NotFound("No customer profile for this login."); // a Customer-role login whose profile row is gone

        return Ok(profile);
    }

    [HttpPut("me")]
    public async Task<ActionResult<CustomerProfileDto>> UpdateMe(CustomerProfileUpdateDto dto)
    {
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized();

        var profile = await _customerService.UpdateProfileAsync(applicationUserId.Value, dto);
        if (profile is null) return NotFound("No customer profile for this login.");

        return Ok(profile); // echo the saved profile back so the client doesn't need a follow-up GET
    }
}
