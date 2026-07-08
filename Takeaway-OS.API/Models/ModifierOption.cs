namespace Takeaway_OS.API.Models;

public class ModifierOption
{
    public int Id { get; set; }
    public int ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool IsActive { get; set; } = true;

    public ModifierGroup ModifierGroup { get; set; } = null!;
}