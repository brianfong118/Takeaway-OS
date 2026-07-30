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

// Shape returned by POST /api/orders. Bundles the created order with the Stripe client secret
// the browser needs to complete payment, so the frontend gets both from one response.
public class OrderCreateResponseDto
{
    public OrderDto Order { get; set; } = null!;
    public string ClientSecret { get; set; } = string.Empty;

    // The guest's read-back key for this order (Order.PublicToken).
    // Returned HERE and nowhere else: this response goes only to whoever just placed the order.
    // Deliberately NOT a field on OrderDto - that shape is served to the Owner's whole-restaurant
    // list, and putting a per-order access key in it would hand out every customer's key at once.
    public Guid PublicToken { get; set; }
}

// Shape returned by GET /api/orders/by-token/{token} - the guest order confirmation page.
// A guest has no login, so the token is the authorisation, which makes this a deliberately
// narrower projection than OrderDto: anyone holding the link sees exactly these fields.
//
// The rule applied: include what the confirmation page renders, exclude what is decoration.
//   - CustomerPhone is absent. The page has no use for it and it is the most abusable field here.
//   - CustomerName is absent for the same reason - "Thanks, <name>" is not worth adding a name to
//     what a forwarded link discloses.
//   - DeliveryAddress IS present, because checking the food is going to the right place is a real
//     function of a confirmation page, not decoration.
//   - CustomerId / DriverId are absent: internal ids, nothing the customer can act on.
public class GuestOrderDto
{
    public int Id { get; set; }            // the human-facing order number, quoted on the phone
    public OrderType OrderType { get; set; }
    public OrderStatus Status { get; set; } // the reason this endpoint exists - live, not a snapshot
    public string DeliveryAddress { get; set; } = string.Empty; // empty on collection orders
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Reuses OrderItemDto: the customer's own basket, so it discloses nothing they didn't submit.
    public List<OrderItemDto> Items { get; set; } = new();

    // Same ComputeTotal as everywhere else, so the receipt can't disagree with what Stripe charged.
    public decimal Total { get; set; }
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