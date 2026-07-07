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

    [HttpPost]
    public async Task<ActionResult<DriverDto>> Create(DriverCreateDto dto)
    {
        var created = await _driverService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

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