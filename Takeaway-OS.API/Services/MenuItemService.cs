using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class MenuItemService : IMenuItemService
{
    private readonly AppDbContext _context;
    public MenuItemService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<MenuItemDto>> GetAllAsync()
    {
        return await _context.MenuItems
            .Include(m => m.Category) // eager-load Category so CategoryName is available below
            .OrderBy(m => m.Name)
            .Select(m => new MenuItemDto
            {
                Id = m.Id,
                CategoryId = m.CategoryId,
                CategoryName = m.Category.Name, // only works because of the .Include() above
                Name = m.Name,
                Description = m.Description,
                Price = m.Price,
                IsAvailable = m.IsAvailable
            })
            .ToListAsync();
    }

    public async Task<MenuItemDto?> GetByIdAsync(int id)
    {
        var menuItem = await _context.MenuItems
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
        // FindAsync (used in CategoryService) can't be combined with .Include(),
        // Use FirstOrDefaultAsync + an explicit id filter instead.

        if (menuItem is null) return null;

        return new MenuItemDto
        {
            Id = menuItem.Id,
            CategoryId = menuItem.CategoryId,
            CategoryName = menuItem.Category.Name,
            Name = menuItem.Name,
            Description = menuItem.Description,
            Price = menuItem.Price,
            IsAvailable = menuItem.IsAvailable
        };
    }

    public async Task<MenuItemDto?> CreateAsync(MenuItemCreateDto dto)
    {
        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId);
        if (!categoryExists) return null; // signals "invalid CategoryId" to the controller

        var menuItem = new MenuItem
        {
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            IsAvailable = dto.IsAvailable
        };

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        // Re-fetch the Category name for the response.
        // menuItem.Category is still null here — we never loaded it, we only set CategoryId.
        var category = await _context.Categories.FindAsync(dto.CategoryId);

        return new MenuItemDto
        {
            Id = menuItem.Id,
            CategoryId = menuItem.CategoryId,
            CategoryName = category!.Name, // safe: confirmed it exists above
            Name = menuItem.Name,
            Description = menuItem.Description,
            Price = menuItem.Price,
            IsAvailable = menuItem.IsAvailable
        };
    }

    public async Task<bool?> UpdateAsync(int id, MenuItemUpdateDto dto)
    {
        var menuItem = await _context.MenuItems.FindAsync(id);
        if (menuItem is null) return false; // item not found

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId);
        if (!categoryExists) return null; // invalid CategoryId

        menuItem.CategoryId = dto.CategoryId;
        menuItem.Name = dto.Name;
        menuItem.Description = dto.Description;
        menuItem.Price = dto.Price;
        menuItem.IsAvailable = dto.IsAvailable;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var menuItem = await _context.MenuItems.FindAsync(id);
        if (menuItem is null) return false;

        _context.MenuItems.Remove(menuItem);
        await _context.SaveChangesAsync();
        return true;
    }
}