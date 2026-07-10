using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DriversController : ControllerBase
{
    private readonly IDriverService _driverService;

    public DriversController(IDriverService driverService)
    {
        _driverService = driverService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DriverDto>>> GetAll()
    {
        return Ok(await _driverService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DriverDto>> GetById(int id)
    {
        var driver = await _driverService.GetByIdAsync(id);
        if (driver is null) return NotFound();
        return Ok(driver);
    }

    // No POST here -> Driver is created via POST /api/auth/register (role: "Driver"),
    // which creates the ApplicationUser login and this profile row together.

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, DriverUpdateDto dto)
    {
        var updated = await _driverService.UpdateAsync(id, dto);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _driverService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}