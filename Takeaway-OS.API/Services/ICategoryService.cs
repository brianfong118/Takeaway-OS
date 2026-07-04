using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

public interface ICategoryService // Interface for category service
{
    Task<List<CategoryDto>> GetAllAsync();
    // returns all categories, as a list of CategoryDto (not Category, bcuz dto> raw entities 
    // Wrapped in Task<> because it's asynchronous 
    // db calls take time; async/await lets the app do other things while waiting instead of blocking a thread
    
    Task<CategoryDto?> GetByIdAsync(int id);
    // returns a single category by id, ? -> or null if not found 

    Task<CategoryDto> CreateAsync(CategoryCreateDto dto);
    // creates a new category from the provided dto
    // returns the created category as a CategoryDto w/ Id assigned by the database

    Task<bool> UpdateAsync(int id, CategoryUpdateDto dto);
    Task<bool> DeleteAsync(int id);
    // return true/false to tell the controller whether the thing existed and was updated/deleted
    // so the controller can decide between NoContent() (success) or NotFound().
    
}