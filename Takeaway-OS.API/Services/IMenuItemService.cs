using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IMenuItemService
{
    // Separate methods, not a bool flag, so a caller can never accidentally leak disabled items by
    // forgetting to pass the flag, endpoint's choice of method is the access decision.

    // Admin reads (Owner): every item, available or not (items can be edited/re-enabled)  
    Task<List<MenuItemDto>> GetAllAsync();
    Task<MenuItemDto?> GetByIdAsync(int id);

    // Public reads (anonymous): available items only -> the soft-delete filter (IsAvailable = true).
    Task<List<MenuItemDto>> GetAvailableAsync();
    Task<MenuItemDto?> GetAvailableByIdAsync(int id);
    Task<MenuItemDto?> CreateAsync(MenuItemCreateDto dto);   // null = invalid CategoryId
    Task<bool?> UpdateAsync(int id, MenuItemUpdateDto dto);  // null = invalid CategoryId, false = item not found, true = success
    Task<bool> DeleteAsync(int id);
}