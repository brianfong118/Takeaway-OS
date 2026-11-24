using System.ComponentModel.DataAnnotations;

namespace Takeaway_OS.API.DTOs;

public class MenuItemDto  // shape returned by list GET requests (lean / no modifiers)
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty; // denormalized for frontend convenience — populated via .Include() at query time, never stored
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}

// single-item detail read, inherits every field from MenuItemDto
public class MenuItemDetailDto : MenuItemDto
{
    public List<ModifierGroupDto> ModifierGroups { get; set; } = new();
}

// Owner's single-item detail read. Same shape as MenuItemDetailDto but a distinct type because the
// two differ in what they're allowed to contain, not in their fields: the public one is filtered
// (available item, active options only), this one is not. Sharing one type would make it far too easy
// to hand the unfiltered projection to the anonymous endpoint by mistake.
public class MenuItemAdminDetailDto : MenuItemDto
{
    // Includes options with IsActive = false, so the owner can see and re-enable a disabled option.
    public List<ModifierGroupDto> ModifierGroups { get; set; } = new();
}

// Validation on the write DTOs below: the owner form already checks these in the browser, but the
// API is the authority. A request from Swagger, a script or a buggy form must not be able to save a
// nameless item or a negative price (which would SUBTRACT from every order containing it).
// Lengths mirror the owner form's maxLength values, so the two can't disagree.
public class MenuItemCreateDto  // shape accepted by POST requests
{
    [Range(1, int.MaxValue)] // 0 is an unset int, not a category; caught here instead of as "Category 0 does not exist"
    public int CategoryId { get; set; } // raw FK only — client picks a category, doesn't invent a name

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    // 0 is allowed (a free side, e.g. prawn crackers); the upper bound only catches a slip like 850 for 8.50.
    //
    // typeof(decimal) is required, not decoration. [Range(0, 1000)] picks the INT overload, which
    // converts the price to an int before comparing, rounding -0.40 to 0, so a negative price under
    // £0.50 would pass. This overload compares as decimal. The limits are strings, parsed in the
    // invariant culture so "0.5" can never be read with a comma decimal separator.
    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public class MenuItemUpdateDto  // shape accepted by PUT requests
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "1000", ParseLimitsInInvariantCulture = true)] // decimal overload: see MenuItemCreateDto
    public decimal Price { get; set; }

    public bool IsAvailable { get; set; }
}

// No Id in create/update, database-generated
// No CategoryName in create/update, derived from CategoryId, never client-supplied