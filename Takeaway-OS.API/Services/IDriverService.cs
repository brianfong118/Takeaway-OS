using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface IDriverService
{
    Task<List<DriverDto>> GetAllAsync();
    Task<DriverDto?> GetByIdAsync(int id);
    // no CreateAsync -> Drivers are created via AuthService.RegisterAsync (role: "Driver"),
    // must have an ApplicationUser login to exist first, a standalone create here can't do that
    Task<bool> UpdateAsync(int id, DriverUpdateDto dto);
    Task<bool> DeleteAsync(int id);
}