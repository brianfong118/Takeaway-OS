namespace Takeaway_OS.API.Models;

public class Order
{
    public int Id { get; set; }

    // Contact/delivery details stay on every Order regardless of guest or not
    // Order is the record of what was actually submitted, not a pointer to a Customer profile.
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;

    // Nullable on purpose -> null means guest checkout, which stays the default path.
    // Populated only when the caller presents a JWT belonging to a Customer profile.
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public OrderType OrderType { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}