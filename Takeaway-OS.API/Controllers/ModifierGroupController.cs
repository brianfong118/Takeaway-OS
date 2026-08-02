using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
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
    [Authorize(Roles = Roles.Owner)]
    public async Task<ActionResult<ModifierGroupDto>> Create(ModifierGroupCreateDto dto)
    {
        var created = await _modifierGroupService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Update(int id, ModifierGroupUpdateDto dto)
    {
        var updated = await _modifierGroupService.UpdateAsync(id, dto);
        if (!updated) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _modifierGroupService.DeleteAsync(id);

        // Named rather than numbered, same reasoning as CategoriesController.Delete. Both causes
        // stay in one sentence because the Restrict FKs fail as one DbUpdateException - telling
        // them apart would cost two extra queries to say something the owner can see on screen.
        if (result == DeleteResult.HasDependents)
        {
            var name = (await _modifierGroupService.GetByIdAsync(id))?.Name;
            var subject = name is null ? $"Group {id}" : $"\"{name}\"";
            return Conflict($"{subject} still has options, or is still used by a dish. Remove its options and untick it from any dish first.");
        }

        return result switch
        {
            DeleteResult.NotFound => NotFound(),
            _ => NoContent()
        };
    }
}