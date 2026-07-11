using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenuItemsController : ControllerBase
{
    private readonly IMenuItemService _menuItemService;

    public MenuItemsController(IMenuItemService menuItemService)
    {
        _menuItemService = menuItemService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MenuItemDto>>> GetAll()
    {
        return Ok(await _menuItemService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MenuItemDto>> GetById(int id)
    {
        var menuItem = await _menuItemService.GetByIdAsync(id);
        if (menuItem is null) return NotFound();
        return Ok(menuItem);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Owner)]
    public async Task<ActionResult<MenuItemDto>> Create(MenuItemCreateDto dto)
    {
        var created = await _menuItemService.CreateAsync(dto);
        if (created is null)
            return BadRequest($"Category with Id {dto.CategoryId} does not exist.");

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Update(int id, MenuItemUpdateDto dto)
    {
        var result = await _menuItemService.UpdateAsync(id, dto);

        if (result is null)
            return BadRequest($"Category with Id {dto.CategoryId} does not exist.");
        if (result == false)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _menuItemService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}