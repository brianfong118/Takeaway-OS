namespace Takeaway_OS.API.Services;

public enum LinkResult
{
    Success,
    MenuItemNotFound,
    ModifierGroupNotFound,
    AlreadyLinked
}

public interface IMenuItemModifierGroupService
{
    Task<LinkResult> LinkAsync(int menuItemId, int modifierGroupId);
    Task<bool> UnlinkAsync(int menuItemId, int modifierGroupId); // false = the pairing didn't exist
}