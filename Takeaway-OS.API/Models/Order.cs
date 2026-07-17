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

    // Nullable -> null = "no driver assigned yet" (every order's starting state/permanent state of every collection order 
    // Set by the Owner via the assignment endpoint, never by the customer placing the order.
    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public OrderType OrderType { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // The id of this order's Stripe PaymentIntent, set when the order is created.
    // Nullable: existing orders predate Stripe, so the column must allow null on those rows.
    // link the webhook uses -> it finds the order by this id and marks it Paid,
    // which is also what makes duplicate webhook deliveries safe (idempotent) to process.
    public string? StripePaymentIntentId { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}