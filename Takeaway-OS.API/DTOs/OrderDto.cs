using System.ComponentModel.DataAnnotations;
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
    public int? DriverId { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();

    // Computed by the service on every read: sum(UnitPrice * Quantity) + sum(modifier PriceDeltas).
    // Never stored on the Orders table — always derived fresh, so it can't drift from the line items.
    public decimal Total { get; set; }
}

// IValidatableObject adds a cross-field rule (see Validate below) on top of the per-property attributes.
public class OrderCreateDto : IValidatableObject  // shape accepted by POST /api/orders
{
    // [Required] on a string rejects null AND empty/whitespace by default (AllowEmptyStrings = false),
    // so an empty CustomerName is a 400, not a blank contact on a real order.
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    // Non-empty only -> deliberately no [Phone] format check
    // A unexpected format shouldn't cost a customer their order. Only need *something* to call back on.
    [Required]
    [MaxLength(30)]
    public string CustomerPhone { get; set; } = string.Empty;

    // Not [Required]: only Delivery orders need an address -> can come from AddressId instead (see Validate below)
    // A per-property attribute can't express either of those conditions.
    [MaxLength(250)]
    public string DeliveryAddress { get; set; } = string.Empty;

    public int? AddressId { get; set; }

    public OrderType OrderType { get; set; }

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;

    // An empty basket is not an order. [MinLength(1)] replaces the old manual Count == 0 check in the service.
    [MinLength(1, ErrorMessage = "Order must contain at least one item.")]
    public List<OrderItemCreateDto> Items { get; set; } = new();

    // No Status field — every new order starts at OrderStatus.Pending, set by the service, not the client.
    // No CreatedAt — server sets this via DateTime.UtcNow, never trusts a client-supplied timestamp.

    // Cross-field validation: attributes above each judge one property in isolation, but "an address is required" depends on OrderType,
    // This runs during model binding, so a failure is a 400 in the same ValidationProblemDetails shape as the attributes 
    // and keying the result to DeliveryAddress lets the frontend highlight exactly that field.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // A delivery order needs an address from EITHER source: a saved AddressId || free text
        // Whether the AddressId actually resolves to one of the caller's addresses is a DB check -> service 
        // here we only know the request didn't supply either
        if (OrderType == OrderType.Delivery
            && AddressId is null
            && string.IsNullOrWhiteSpace(DeliveryAddress))
        {
            yield return new ValidationResult(
                "Delivery orders require a delivery address — select a saved address or enter one.",
                new[] { nameof(DeliveryAddress) });
        }
    }
}

// Separate, narrow DTO for the staff status-update endpoint
// NOT reusing OrderUpdateDto (there isn't one: customer/address fields aren't
// editable after placement, only Status changes, via its own endpoint).
public class OrderStatusUpdateDto
{
    public OrderStatus Status { get; set; }
}

// Body for PUT /api/orders/{id}/driver (Owner-only).
// DriverId is nullable: the same route both assigns and unassigns.
//   { "driverId": 3 }    -> assign driver 3
//   { "driverId": null } -> clear the assignment
// Making unassign null instead of delete keeps the operation idempotent 
// The order id is NOT a field here: it's the {id} route segment, so the body can't disagree with the URL.
public class OrderDriverAssignmentDto
{
    public int? DriverId { get; set; }
}