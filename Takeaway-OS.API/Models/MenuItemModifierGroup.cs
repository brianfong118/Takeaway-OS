namespace Takeaway_OS.API.Models;

public class MenuItemModifierGroup
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public int ModifierGroupId { get; set; }

    public MenuItem MenuItem { get; set; } = null!;
    public ModifierGroup ModifierGroup { get; set; } = null!;
}