using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
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

    // Owner-only write endpoints (edit the schedule, trigger a holiday closure) aren't built yet;
    // hours go in via SQL/seed for now.

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
}
