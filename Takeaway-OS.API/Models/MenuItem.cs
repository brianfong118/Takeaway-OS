namespace Takeaway_OS.API.Models;

public class MenuItem
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;

    public Category Category { get; set; } = null!;
    public ICollection<MenuItemModifierGroup> MenuItemModifierGroups { get; set; } = new List<MenuItemModifierGroup>();
}