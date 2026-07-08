using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModifierOptionsController : ControllerBase
{
    private readonly IModifierOptionService _modifierOptionService;

    public ModifierOptionsController(IModifierOptionService modifierOptionService)
    {
        _modifierOptionService = modifierOptionService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ModifierOptionDto>>> GetAll()
    {
        return Ok(await _modifierOptionService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ModifierOptionDto>> GetById(int id)
    {
        var option = await _modifierOptionService.GetByIdAsync(id);
        if (option is null) return NotFound();
        return Ok(option);
    }

    [HttpPost]
    public async Task<ActionResult<ModifierOptionDto>> Create(ModifierOptionCreateDto dto)
    {
        var created = await _modifierOptionService.CreateAsync(dto);
        if (created is null)
            return BadRequest($"ModifierGroup with Id {dto.ModifierGroupId} does not exist.");

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ModifierOptionUpdateDto dto)
    {
        var result = await _modifierOptionService.UpdateAsync(id, dto);

        if (result is null)
            return BadRequest($"ModifierGroup with Id {dto.ModifierGroupId} does not exist.");
        if (result == false)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _modifierOptionService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}