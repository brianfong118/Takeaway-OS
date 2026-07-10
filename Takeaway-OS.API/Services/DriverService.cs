using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class DriverService : IDriverService
{
    private readonly AppDbContext _context;
    public DriverService(AppDbContext context)
    {
        _context = context;
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

        _context.Drivers.Remove(driver);
        await _context.SaveChangesAsync();
        return true;
    }
}