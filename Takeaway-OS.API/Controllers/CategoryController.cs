using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController] // tells ASP.NET Core this class is a Web API controller, not an MVC view controller 
[Route("api/[controller]")] // sets the base route (URL prefix) for all actions in this controller to "api/categories" 
                            // the [controller] token is replaced with the controller name minus "Controller"
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService; // dependency injection of the Icategory service (interface for category service)

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet] // maps method to GET /api/categories
    public async Task<ActionResult<List<CategoryDto>>> GetAll() // returns a list of CategoryDto objects wrapped in an ActionResult, which allows for returning HTTP status codes along with the data
    {
        return Ok(await _categoryService.GetAllAsync()); // Ok(..) wraps result in a 200 OK status 
    }

    [HttpGet("{id}")] // maps method to GET /api/categories/{id}, where {id} is a route parameter that will be passed to the method
    public async Task<ActionResult<CategoryDto>> GetById(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category is null) return NotFound(); // ? in service -> if the service returned null (category doesn't exist), the controller translates that into a proper 404 Not Found HTTP response — not a crash, not an empty 200.
        return Ok(category);
    }

    [HttpPost] // maps method to POST /api/categories
    [Authorize(Roles = Roles.Owner)] // menu management is Owner-only
    public async Task<ActionResult<CategoryDto>> Create(CategoryCreateDto dto) // accepts a CategoryCreateDto object from the request body bcuz of [ApiController] attribute, which automatically binds JSON in the request body to the dto parameter
    {
        var created = await _categoryService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created); // returns a 201 Created response with a Location header pointing to the newly created resource (GET /api/categories/{id})
    }

    [HttpPut("{id}")] // maps method to PUT /api/categories/{id}
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Update(int id, CategoryUpdateDto dto) // IActionResult -> because the method doesn't return any data, just a status code (204 No Content or 404 Not Found)
    {
        var updated = await _categoryService.UpdateAsync(id, dto);
        if (!updated) return NotFound();  // 404
        return NoContent(); // 204 No Content, standard response for a successful PUT request that doesn't return any data
    }

    [HttpDelete("{id}")] // maps method to DELETE /api/categories/{id}
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _categoryService.DeleteAsync(id);

        // Named, not numbered. This message is shown to the owner beside a row labelled
        // "Starters", and an id they never see anywhere else in the UI tells them nothing.
        // The extra read only happens on this path, and the row is certainly still there:
        // the delete was just refused because of it.
        if (result == DeleteResult.HasDependents)
        {
            var name = (await _categoryService.GetByIdAsync(id))?.Name;
            return Conflict(name is null
                ? $"Category {id} still has menu items. Move or delete them first."
                : $"\"{name}\" still has menu items. Move or delete them first.");
        }

        return result switch
        {
            DeleteResult.NotFound => NotFound(),
            _ => NoContent()
        };
    }
}