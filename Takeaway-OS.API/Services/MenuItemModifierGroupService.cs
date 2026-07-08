using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class MenuItemModifierGroupService : IMenuItemModifierGroupService
{
    private readonly AppDbContext _context;
    public MenuItemModifierGroupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LinkResult> LinkAsync(int menuItemId, int modifierGroupId)
    {
        var menuItemExists = await _context.MenuItems.AnyAsync(m => m.Id == menuItemId);
        if (!menuItemExists) return LinkResult.MenuItemNotFound;

        var groupExists = await _context.ModifierGroups.AnyAsync(g => g.Id == modifierGroupId);
        if (!groupExists) return LinkResult.ModifierGroupNotFound;

        var alreadyLinked = await _context.MenuItemModifierGroups
            .AnyAsync(l => l.MenuItemId == menuItemId && l.ModifierGroupId == modifierGroupId);
        if (alreadyLinked) return LinkResult.AlreadyLinked;

        _context.MenuItemModifierGroups.Add(new MenuItemModifierGroup
        {
            MenuItemId = menuItemId,
            ModifierGroupId = modifierGroupId
        });
        await _context.SaveChangesAsync();

        return LinkResult.Success;
    }

    public async Task<bool> UnlinkAsync(int menuItemId, int modifierGroupId)
    {
        var link = await _context.MenuItemModifierGroups
            .FirstOrDefaultAsync(l => l.MenuItemId == menuItemId && l.ModifierGroupId == modifierGroupId);
        if (link is null) return false;

        _context.MenuItemModifierGroups.Remove(link);
        await _context.SaveChangesAsync();
        return true;
    }
}