using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

// Wraps CreateAsync's outcome
// Order-creation touches MenuItems, ModifierOptions, ModifierGroups all at once,
// therefore several distinct ways it can fail
// but all the *validation* failures mean the same thing to the controller (400 Bad Request),
// so a single Error message covers them -> No enum needed
public class OrderCreateResult
{
    public OrderDto? Order { get; set; }   // null = order refused, see Error
    public string? Error { get; set; }     // set only when Order is null

    // The one refusal that ISN'T a 400: the basket is perfectly valid, but shop closed.
    // maps to 409 Conflict instead
    // A bool rather than an enum because closed-vs-invalid is the only split that exists.
    public bool RestaurantClosed { get; set; }
}

// UpdateStatusAsync's outcome DOES need an enum: OrderNotFound (404) and
// InvalidTransition (400) are different responses, same shape as LinkResult.
public enum OrderStatusUpdateResult
{
    Success,
    OrderNotFound,
    InvalidTransition   // covers: backwards/skipped transitions, and manually setting Paid (Stripe-webhook-only)
}

public enum DriverAssignmentResult
{
    Success,
    OrderNotFound, // 404: the {id} in the URL names no order
    DriverNotFound, // 400: the URL is fine, but the body points at a driver that doesn't exist
    NotDeliveryOrder // 400: the order exists but is Collection, so a driver makes no sense on it
}

public interface IOrderService
{
    Task<List<OrderDto>> GetAllAsync();
    Task<OrderDto?> GetByIdAsync(int id);

    // Every order placed by the Customer profile attached to this Identity login.
    // Takes an ApplicationUserId (what the JWT actually carries), not a Customer.Id - the caller
    // can't be trusted to tell us which customer they are, so the service resolves it itself.
    Task<List<OrderDto>> GetForCustomerAsync(int applicationUserId);

    // Returns null both when the order doesn't exist AND when it belongs to someone else
    // so controller's 404 can't be used to probe which order IDs exist.
    Task<OrderDto?> GetByIdForCustomerAsync(int id, int applicationUserId);

    // applicationUserId is null for guest checkout, which stays the default path.
    // Separate parameter rather than a field on OrderCreateDto BECAUSE it comes from the validated JWT 
    // putting it in the request body would let a caller claim any customer's ID.
    Task<OrderCreateResult> CreateAsync(OrderCreateDto dto, int? applicationUserId);
    Task<OrderStatusUpdateResult> UpdateStatusAsync(int id, OrderStatusUpdateDto dto);

    // Owner-only: set (or clear, when dto.DriverId is null) the driver carrying a delivery order.
    // Takes the whole DTO rather than a bare int? so the "assign vs unassign" intent is explicit at the call site.
    Task<DriverAssignmentResult> AssignDriverAsync(int id, OrderDriverAssignmentDto dto);

    // No DeleteAsync: orders are never deleted, only moved to Cancelled via UpdateStatusAsync
}
