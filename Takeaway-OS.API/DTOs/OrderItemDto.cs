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
    public int MenuItemId { get; set; }   // client picks an existing menu item — service snapshots ItemName/UnitPrice from it
    public int Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<int> ModifierOptionIds { get; set; } = new();  // raw IDs only — service validates + snapshots each one
}

// No OrderItemUpdateDto: order lines aren't edited after creation 
// if a customer's order is wrong, staff cancel/recreate it via Order status,
// they don't PATCH individual line items. Revisit only if a real need shows up.