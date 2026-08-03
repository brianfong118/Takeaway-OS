using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IRestaurantSettingsService _settingsService;

    public SettingsController(IRestaurantSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet] // GET /api/settings -> read by the basket/checkout to show the fee before ordering
    public async Task<ActionResult<RestaurantSettingsDto>> Get()
    {
        return Ok(await _settingsService.GetAsync());
    }

    [HttpPut] // PUT /api/settings
    [Authorize(Roles = Roles.Owner)]
    public async Task<ActionResult<RestaurantSettingsDto>> Update(RestaurantSettingsUpdateDto dto)
    {
        await _settingsService.UpdateAsync(dto);

        // Returns the saved settings rather than 204, same call as the closure PUT: the owner
        // sees the value the server actually holds, not the one their form thinks it sent.
        return Ok(await _settingsService.GetAsync());
    }
}
