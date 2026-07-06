namespace Takeaway_OS.API.DTOs;

public class MenuItemDto  // shape returned by GET requests
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty; // denormalized for frontend convenience — populated via .Include() at query time, never stored
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}

public class MenuItemCreateDto  // shape accepted by POST requests
{
    public int CategoryId { get; set; } // raw FK only — client picks a category, doesn't invent a name
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class MenuItemUpdateDto  // shape accepted by PUT requests
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}

// No Id in create/update, database-generated
// No CategoryName in create/update, derived from CategoryId, never client-supplied