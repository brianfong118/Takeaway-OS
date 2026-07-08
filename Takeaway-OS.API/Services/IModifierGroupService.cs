using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IModifierGroupService
{
    Task<List<ModifierGroupDto>> GetAllAsync();
    Task<ModifierGroupDto?> GetByIdAsync(int id);
    Task<ModifierGroupDto> CreateAsync(ModifierGroupCreateDto dto);
    Task<bool> UpdateAsync(int id, ModifierGroupUpdateDto dto);

    Task<DeleteResult> DeleteAsync(int id);
    // DeleteResult, not bool: blocked (409) if the group still has ModifierOptions or is still
    // linked to a MenuItem, same reasoning as ICategoryService.DeleteAsync.
}