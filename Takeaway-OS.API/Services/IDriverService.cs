using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IDriverService
{
    Task<List<DriverDto>> GetAllAsync();
    Task<DriverDto?> GetByIdAsync(int id);
    Task<DriverDto> CreateAsync(DriverCreateDto dto);
    // no invalid-FK case here (no relationships) -> CreateAsync/UpdateAsync return plain types, not nullable
    Task<bool> UpdateAsync(int id, DriverUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}