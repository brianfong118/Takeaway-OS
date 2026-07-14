using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Extensions;  // User.GetApplicationUserId()
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

// The address book is always the CALLER'S own - there is no /api/customers/{id}/addresses route,
// and no customer id anywhere in a route or body. The owning customer comes only from the JWT
// there's no id for anyone to tamper with in the first place
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Customer)] // on the class, so every action below inherits it and no new one can be added unguarded
public class AddressesController : ControllerBase
{
    private readonly IAddressService _addressService;

    public AddressesController(IAddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AddressDto>>> GetMine()
    {
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized();

        return Ok(await _addressService.GetForCustomerAsync(applicationUserId.Value));
    }

    [HttpPost]
    public async Task<ActionResult<AddressDto>> Create(AddressCreateDto dto)
    {
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized();

        var address = await _addressService.CreateAsync(dto, applicationUserId.Value);
        if (address is null) return NotFound("No customer profile for this login."); // a Customer-role login whose profile row is gone

        // No CreatedAtAction: there's no GET /api/addresses/{id} to point a Location header at.
        // A customer reads their address book as a whole list, never one address by id.
        return Created(string.Empty, address); 
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AddressDto>> Update(int id, AddressUpdateDto dto)
    {
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized();

        var address = await _addressService.UpdateAsync(id, dto, applicationUserId.Value);
        if (address is null) return NotFound(); // also the answer when the address is someone else's - see IAddressService

        return Ok(address);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized();

        var deleted = await _addressService.DeleteAsync(id, applicationUserId.Value);
        if (!deleted) return NotFound();

        return NoContent();
    }
}
