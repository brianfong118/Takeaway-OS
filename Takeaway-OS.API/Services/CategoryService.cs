using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _context; 
    public CategoryService(AppDbContext context)
    {
        _context = context;
    }
    // AppDbContext is what actually interacts w/PostgreSQL database. 
    // Dependency injection of the AppDbContext 
    // The service then uses the context to perform CRUD operations on the Category entity.
    // AppDbContext supplied by DI container in Program.cs

    public async Task<List<CategoryDto>> GetAllAsync()
    {
        return await //await -> asynchronous operation; don't block the thread while waiting for the database to respond
            _context.Categories // access the Categories DbSet in the AppDbContext
            .OrderBy(c => c.DisplayOrder) 
            .Select(c => new CategoryDto // entity to DTO mapping
            {                            // c is from db
                Id = c.Id,
                Name = c.Name,
                DisplayOrder = c.DisplayOrder
            })
            .ToListAsync(); 
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var category = await _context.Categories.FindAsync(id); // FindAsync -> find by primary key (Id)
        if (category is null) return null;

        return new CategoryDto // IF found, map the entity to a DTO and return it
        {
            Id = category.Id,
            Name = category.Name,
            DisplayOrder = category.DisplayOrder
        };
    }

    public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto)
    {
        var category = new Category  // DTO → entity
        { 
            Name = dto.Name,
            DisplayOrder = dto.DisplayOrder
        };

        _context.Categories.Add(category); // Add the new category to the context
        await _context.SaveChangesAsync(); // SaveChangesAsync persists the changes to the database + assigns the generated Id to the category entity

        return new CategoryDto // entity → DTO, so that controller can return the created category w/ its new Id to the client
        {
            Id = category.Id,
            Name = category.Name,
            DisplayOrder = category.DisplayOrder
        };
    }

    public async Task<bool> UpdateAsync(int id, CategoryUpdateDto dto)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category is null) return false;

        category.Name = dto.Name;
        category.DisplayOrder = dto.DisplayOrder;

        await _context.SaveChangesAsync(); // SaveChangesAsync persists the changes to the database
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category is null) return false;

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(); // SaveChangesAsync persists the changes to the database
        return true;
    }
}