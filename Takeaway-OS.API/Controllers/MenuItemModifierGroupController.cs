using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/menuitems/{menuItemId}/modifiergroups")]
[Authorize(Roles = Roles.Owner)] // linking/unlinking modifier groups is menu management, Owner-only
public class MenuItemModifierGroupsController : ControllerBase
{
    private readonly IMenuItemModifierGroupService _linkService;

    public MenuItemModifierGroupsController(IMenuItemModifierGroupService linkService)
    {
        _linkService = linkService;
    }

    [HttpPost("{modifierGroupId}")]
    public async Task<IActionResult> Link(int menuItemId, int modifierGroupId)
    {
        var result = await _linkService.LinkAsync(menuItemId, modifierGroupId);

        return result switch
        {
            LinkResult.MenuItemNotFound => NotFound($"MenuItem with Id {menuItemId} does not exist."),
            LinkResult.ModifierGroupNotFound => NotFound($"ModifierGroup with Id {modifierGroupId} does not exist."),
            LinkResult.AlreadyLinked => Conflict("This ModifierGroup is already linked to this MenuItem."), // 409 Conflict
            _ => NoContent()
        };
    }

    [HttpDelete("{modifierGroupId}")]
    public async Task<IActionResult> Unlink(int menuItemId, int modifierGroupId)
    {
        var unlinked = await _linkService.UnlinkAsync(menuItemId, modifierGroupId);
        if (!unlinked) return NotFound();
        return NoContent();
    }
}