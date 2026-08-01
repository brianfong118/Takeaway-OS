using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IMenuItemService
{
    // Separate methods, not a bool flag, so a caller can never accidentally leak disabled items by
    // forgetting to pass the flag, endpoint's choice of method is the access decision.

    // Admin reads (Owner): every item, available or not (items can be edited/re-enabled)
    Task<List<MenuItemDto>> GetAllAsync();
    // Single-item admin read: the item PLUS its modifier groups, unfiltered on both counts —
    // a disabled item still resolves, and inactive options are included so they can be re-enabled.
    Task<MenuItemAdminDetailDto?> GetAdminDetailByIdAsync(int id);

    // Public reads (anonymous): available items only -> the soft-delete filter (IsAvailable = true).
    Task<List<MenuItemDto>> GetAvailableAsync();
    // Single-item detail read: the lean item PLUS its modifier groups (active options only)
    Task<MenuItemDetailDto?> GetAvailableDetailByIdAsync(int id);
    Task<MenuItemDto?> CreateAsync(MenuItemCreateDto dto);   // null = invalid CategoryId
    Task<bool?> UpdateAsync(int id, MenuItemUpdateDto dto);  // null = invalid CategoryId, false = item not found, true = success
    Task<bool> DeleteAsync(int id);
}