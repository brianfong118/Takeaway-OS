using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/[controller]")] 
public class OpeningHoursController : ControllerBase
{
    private readonly IBusinessHoursService _businessHoursService;

    public OpeningHoursController(IBusinessHoursService businessHoursService)
    {
        _businessHoursService = businessHoursService;
    }

    [HttpGet] // GET /api/openinghours
    public async Task<ActionResult<List<OpeningHoursDto>>> GetSchedule()
    {
        return Ok(await _businessHoursService.GetScheduleAsync());
    }

    [HttpGet("status")] // GET /api/openinghours/status — what the "we're closed" banner polls
    public async Task<ActionResult<RestaurantStatusDto>> GetStatus()
    {
        return Ok(await _businessHoursService.GetStatusAsync());
    }

    [HttpPost] // POST /api/openinghours
    [Authorize(Roles = Roles.Owner)]
    public async Task<ActionResult<OpeningHoursDto>> Create(OpeningHoursCreateDto dto)
    {
        var result = await _businessHoursService.CreateWindowAsync(dto);

        // 400: an overlapping or zero-length window IS a bad request 
        if (result.Window is null) return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetSchedule), result.Window); // no GetById on this controller, schedule is small and always fetched whole
    }

    [HttpPut("{id}")] // PUT /api/openinghours/{id}
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Update(int id, OpeningHoursUpdateDto dto)
    {
        var result = await _businessHoursService.UpdateWindowAsync(id, dto);

        if (result.NotFound) return NotFound();
        if (result.Window is null) return BadRequest(result.Error);

        return NoContent();
    }

    [HttpDelete("{id}")] // DELETE /api/openinghours/{id}
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Delete(int id)
    {
        // Deleting every window for a day is legal — it just means "closed that day".
        var deleted = await _businessHoursService.DeleteWindowAsync(id);
        if (!deleted) return NotFound();

        return NoContent();
    }

    // PUT /api/openinghours/closure — the holiday override.
    [HttpPut("closure")]
    [Authorize(Roles = Roles.Owner)]
    public async Task<ActionResult<RestaurantStatusDto>> SetClosure(ClosureUpdateDto dto)
    {
        await _businessHoursService.SetClosureAsync(dto);

        // Returns the resulting status rather than 204
        // owner can see closure reason, not just "we're closed" like the public banner.
        return Ok(await _businessHoursService.GetStatusAsync());
    }
}
