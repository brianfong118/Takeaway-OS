using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.DTOs;

public class OrderDto  // shape returned by GET requests
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public OrderType OrderType { get; set; }
    public OrderStatus Status { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // null = placed as a guest. Lets the Owner's order list tell an account order from a guest one.
    public int? CustomerId { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();

    // Computed by the service on every read: sum(UnitPrice * Quantity) + sum(modifier PriceDeltas).
    // Never stored on the Orders table — always derived fresh, so it can't drift from the line items.
    public decimal Total { get; set; }
}

public class OrderCreateDto  // shape accepted by POST /api/orders
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public OrderType OrderType { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<OrderItemCreateDto> Items { get; set; } = new();

    // No Status field — every new order starts at OrderStatus.Pending, set by the service, not the client.
    // No CreatedAt — server sets this via DateTime.UtcNow, never trusts a client-supplied timestamp.
}

// Separate, narrow DTO for the staff status-update endpoint
// NOT reusing OrderUpdateDto (there isn't one: customer/address fields aren't
// editable after placement, only Status changes, via its own endpoint).
public class OrderStatusUpdateDto
{
    public OrderStatus Status { get; set; }
}