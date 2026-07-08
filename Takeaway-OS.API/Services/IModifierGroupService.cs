using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IModifierGroupService
{
    Task<List<ModifierGroupDto>> GetAllAsync();
    Task<ModifierGroupDto?> GetByIdAsync(int id);
    Task<ModifierGroupDto> CreateAsync(ModifierGroupCreateDto dto);
    Task<bool> UpdateAsync(int id, ModifierGroupUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}