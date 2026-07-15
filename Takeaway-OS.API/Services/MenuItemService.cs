using System.Linq.Expressions;
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

    // The entity->DTO shape, defined once and reused by every read below.
    // It's an Expression (not a plain lambda) so EF Core translates it into SQL 
    // the m.Category.Name reference becomes a JOIN, which is why none of the reads need a separate .Include(m => m.Category).
    private static readonly Expression<Func<MenuItem, MenuItemDto>> ToDto = m => new MenuItemDto
    {
        Id = m.Id,
        CategoryId = m.CategoryId,
        CategoryName = m.Category.Name,
        Name = m.Name,
        Description = m.Description,
        Price = m.Price,
        IsAvailable = m.IsAvailable
    };

    // --- Admin reads (Owner): no availability filter, so disabled items are visible for editing. ---
    public async Task<List<MenuItemDto>> GetAllAsync()
    {
        return await _context.MenuItems
            .OrderBy(m => m.Name)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<MenuItemDto?> GetByIdAsync(int id)
    {
        return await _context.MenuItems
            .Where(m => m.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync();
    }

    // --- Public reads (anonymous): the same queries with the IsAvailable = true soft-delete filter. ---
    public async Task<List<MenuItemDto>> GetAvailableAsync()
    {
        return await _context.MenuItems
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.Name)
            .Select(ToDto)
            .ToListAsync();
    }

    public async Task<MenuItemDto?> GetAvailableByIdAsync(int id)
    {
        return await _context.MenuItems
            .Where(m => m.Id == id && m.IsAvailable)
            .Select(ToDto)
            .FirstOrDefaultAsync();
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