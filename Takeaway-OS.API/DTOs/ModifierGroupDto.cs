using System.ComponentModel.DataAnnotations;

namespace Takeaway_OS.API.DTOs;

public class ModifierGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public bool IsRequired { get; set; }
    public List<ModifierOptionDto> Options { get; set; } = new();
}

// Why these rules matter more than most: a misconfigured group doesn't fail when it's SAVED, it fails
// later, on the customer, as an item that can never be ordered (e.g. MaxSelect below MinSelect, so no
// selection can ever satisfy both). The owner form checks this in the browser, but the API is the
// authority, so the same rules live here.
public class ModifierGroupCreateDto : IValidatableObject
{
    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 50)]
    public int MinSelect { get; set; }

    // At least 1: a group that allows zero selections offers nothing, and a required group with a
    // maximum of 0 is impossible to satisfy. 50 is just a sanity ceiling.
    [Range(1, 50)]
    public int MaxSelect { get; set; }

    public bool IsRequired { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ModifierGroupRules.Validate(MinSelect, MaxSelect);
}

public class ModifierGroupUpdateDto : IValidatableObject
{
    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 50)]
    public int MinSelect { get; set; }

    [Range(1, 50)]
    public int MaxSelect { get; set; }

    public bool IsRequired { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ModifierGroupRules.Validate(MinSelect, MaxSelect);
}

// The cross-field rule both DTOs above share. Written once here rather than copied into each, so
// Create and Update can't drift apart, while the two DTOs themselves stay separate types (the
// codebase's convention, so one endpoint's request shape can change without the other's).
internal static class ModifierGroupRules
{
    public static IEnumerable<ValidationResult> Validate(int minSelect, int maxSelect)
    {
        if (maxSelect < minSelect)
        {
            yield return new ValidationResult(
                "MaxSelect cannot be less than MinSelect: no selection could satisfy both.",
                new[] { nameof(ModifierGroupCreateDto.MaxSelect) });
        }
    }
}