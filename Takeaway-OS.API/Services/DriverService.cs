using Microsoft.AspNetCore.Identity; // UserManager<ApplicationUser> - deleting a driver means deleting their login
using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class DriverService : IDriverService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    // A Driver is two rows created together in AuthService.RegisterAsync: ApplicationUser login + linked Driver profile
    // Deletion has to remove both, so we need UserManager here too.
    public DriverService(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<DriverDto>> GetAllAsync()
    {
        return await _context.Drivers
            .OrderBy(d => d.Name)
            .Select(d => new DriverDto
            {
                Id = d.Id,
                Name = d.Name,
                Phone = d.Phone,
                IsAvailable = d.IsAvailable
            })
            .ToListAsync();
    }

    public async Task<DriverDto?> GetByIdAsync(int id)
    {
        var driver = await _context.Drivers.FindAsync(id);
        if (driver is null) return null;

        return new DriverDto
        {
            Id = driver.Id,
            Name = driver.Name,
            Phone = driver.Phone,
            IsAvailable = driver.IsAvailable
        };
    }

    public async Task<bool> UpdateAsync(int id, DriverUpdateDto dto)
    {
        var driver = await _context.Drivers.FindAsync(id);
        if (driver is null) return false;

        driver.Name = dto.Name;
        driver.Phone = dto.Phone;
        driver.IsAvailable = dto.IsAvailable;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var driver = await _context.Drivers.FindAsync(id);
        if (driver is null) return false;

        // Delete the LOGIN, not just the profile. The Drivers -> AspNetUsers FK is ON DELETE CASCADE,
        // so removing the ApplicationUser takes the Driver row and its AspNetUserRoles entries with it.
        // The Orders -> Drivers FK is ON DELETE SET NULL, so any orders this driver
        // carried survive with DriverId nulled (a delivered order is a permanent record — see AppDbContext).
        var user = await _userManager.FindByIdAsync(driver.ApplicationUserId.ToString());
        if (user is not null)
        {
            // UserManager.DeleteAsync (not _context.SaveChanges) goes through Identity -> triggers the DB-level cascade above
            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded;
        }

        // Defensive fallback: a Driver can't normally outlive its login (the FK requires one), but if the
        // data ever reached that state, still remove the profile row so the delete does something coherent.
        _context.Drivers.Remove(driver);
        await _context.SaveChangesAsync();
        return true;
    }
}