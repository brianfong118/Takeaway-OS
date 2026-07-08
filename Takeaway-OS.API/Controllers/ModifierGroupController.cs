using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModifierGroupsController : ControllerBase
{
    private readonly IModifierGroupService _modifierGroupService;

    public ModifierGroupsController(IModifierGroupService modifierGroupService)
    {
        _modifierGroupService = modifierGroupService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ModifierGroupDto>>> GetAll()
    {
        return Ok(await _modifierGroupService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ModifierGroupDto>> GetById(int id)
    {
        var group = await _modifierGroupService.GetByIdAsync(id);
        if (group is null) return NotFound();
        return Ok(group);
    }

    [HttpPost]
    public async Task<ActionResult<ModifierGroupDto>> Create(ModifierGroupCreateDto dto)
    {
        var created = await _modifierGroupService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ModifierGroupUpdateDto dto)
    {
        var updated = await _modifierGroupService.UpdateAsync(id, dto);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _modifierGroupService.DeleteAsync(id);

        return result switch
        {
            DeleteResult.NotFound => NotFound(),
            DeleteResult.HasDependents => Conflict($"ModifierGroup {id} still has ModifierOptions or is still linked to a MenuItem. Delete/unlink them first."), // 409
            _ => NoContent()
        };
    }
}