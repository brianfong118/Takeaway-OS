using System.ComponentModel.DataAnnotations;

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
    [Range(1, int.MaxValue)]
    public int ModifierGroupId { get; set; }

    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    // Never negative. A "discount" option would subtract from the line, and selected enough times
    // could push an order's total down (or below zero). 0 is the normal price of a "No onions" option.
    // The decimal overload of [Range], because the int one rounds -0.40 to 0 and lets it through
    // (see MenuItemCreateDto.Price).
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal PriceDelta { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ModifierOptionUpdateDto
{
    [Range(1, int.MaxValue)]
    public int ModifierGroupId { get; set; }

    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal PriceDelta { get; set; }

    public bool IsActive { get; set; }
}