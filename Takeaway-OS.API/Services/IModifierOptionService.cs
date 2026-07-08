using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IModifierOptionService
{
    Task<List<ModifierOptionDto>> GetAllAsync();
    Task<ModifierOptionDto?> GetByIdAsync(int id);
    Task<ModifierOptionDto?> CreateAsync(ModifierOptionCreateDto dto);   // null = invalid ModifierGroupId
    Task<bool?> UpdateAsync(int id, ModifierOptionUpdateDto dto);   // null = invalid ModifierGroupId, false = not found, true = success
    Task<bool> DeleteAsync(int id);
}