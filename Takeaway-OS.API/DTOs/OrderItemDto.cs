using System.ComponentModel.DataAnnotations;

namespace Takeaway_OS.API.DTOs;

public class OrderItemDto  // shape returned inside GET /api/orders responses
{
    public int Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<OrderItemModifierDto> Modifiers { get; set; } = new();
}

public class OrderItemCreateDto  // one line item, nested inside OrderCreateDto.Items
{
    // A real menu item id is +ve; 0 is the un-set default for an int
    // reject it here rather than continue to a "MenuItem 0 does not exist" lookup in the service.
    [Range(1, int.MaxValue)]
    public int MenuItemId { get; set; }   // client picks an existing menu item — service snapshots ItemName/UnitPrice from it

    // [Range] fails ModelState, which [ApiController] turns into a 400.
    [Range(1, 5)]
    public int Quantity { get; set; }

    [MaxLength(500)] // bounds an otherwise unbounded free-text field
    public string Notes { get; set; } = string.Empty;

    public List<int> ModifierOptionIds { get; set; } = new();  // raw IDs only — service validates + snapshots each one
}

// No OrderItemUpdateDto: order lines aren't edited after creation 
// if a customer's order is wrong, staff cancel/recreate it via Order status,
// they don't PATCH individual line items. Revisit only if a real need shows up.