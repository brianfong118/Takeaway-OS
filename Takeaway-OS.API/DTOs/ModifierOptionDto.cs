namespace Takeaway_OS.API.DTOs;

public class ModifierOptionDto
{
    public int Id { get; set; }
    public int ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool IsActive { get; set; }
}

public class ModifierOptionCreateDto
{
    public int ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ModifierOptionUpdateDto
{
    public int ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool IsActive { get; set; }
}