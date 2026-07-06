using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IMenuItemService
{
    Task<List<MenuItemDto>> GetAllAsync();
    Task<MenuItemDto?> GetByIdAsync(int id);
    Task<MenuItemDto?> CreateAsync(MenuItemCreateDto dto);   // null = invalid CategoryId
    Task<bool?> UpdateAsync(int id, MenuItemUpdateDto dto);  // null = invalid CategoryId, false = item not found, true = success
    Task<bool> DeleteAsync(int id);
}