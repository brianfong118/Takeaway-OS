using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/[controller]")] // -> /api/deliveryareas
public class DeliveryAreasController : ControllerBase
{
    private readonly IDeliveryAreaService _deliveryAreaService;

    public DeliveryAreasController(IDeliveryAreaService deliveryAreaService)
    {
        _deliveryAreaService = deliveryAreaService;
    }

    // GET /api/deliveryareas
    [HttpGet]
    public async Task<ActionResult<List<DeliveryAreaDto>>> GetAll()
    {
        return Ok(await _deliveryAreaService.GetAllAsync());
    }

    [HttpPost] // POST /api/deliveryareas
    [Authorize(Roles = Roles.Owner)]
    public async Task<ActionResult<DeliveryAreaDto>> Create(DeliveryAreaCreateDto dto)
    {
        var result = await _deliveryAreaService.CreateAsync(dto);

        // The switch is why CreateAsync returns an enum: these are three different responses.
        return result.Outcome switch
        {
            // 400 = a malformed district is the Owner's typo, and they can retype it.
            DeliveryAreaCreateOutcome.InvalidFormat => BadRequest(
                $"'{dto.OutwardCode}' is not a valid postcode district. Enter the part before the space, e.g. E1 or SW1A."),

            // 409 = nothing wrong with the request, it collides with a row that already
            // exists. Same use of 409 as a Restrict-blocked delete.
            DeliveryAreaCreateOutcome.Duplicate => Conflict(
                $"'{dto.OutwardCode}' is already in the delivery area."),

            // CreatedAtAction points at GetAll rather than a GetById, because there is no
            // by-id read on this controller (list is short and always fetched whole)
            _ => CreatedAtAction(nameof(GetAll), result.Area)
        };
    }

    [HttpDelete("{id}")] // DELETE /api/deliveryareas/{id}
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _deliveryAreaService.DeleteAsync(id);

        return deleted ? NoContent() : NotFound();
    }
}
