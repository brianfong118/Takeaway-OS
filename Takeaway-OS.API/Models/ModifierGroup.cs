namespace Takeaway_OS.API.Models;

public class ModifierGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public bool IsRequired { get; set; }

    public ICollection<ModifierOption> ModifierOptions { get; set; } = new List<ModifierOption>();
    public ICollection<MenuItemModifierGroup> MenuItemModifierGroups { get; set; } = new List<MenuItemModifierGroup>();
}