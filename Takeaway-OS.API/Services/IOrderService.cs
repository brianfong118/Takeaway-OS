using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

// Wraps CreateAsync's outcome
// Order-creation touches MenuItems, ModifierOptions, ModifierGroups all at once,
// therefore several distinct ways it can fail 
// but every one of them means the same thing to the controller (400 Bad Request),
// so a single Error message is enough -> No enum needed 
public class OrderCreateResult
{
    public OrderDto? Order { get; set; }   // null = validation failed, see Error
    public string? Error { get; set; }     // set only when Order is null
}

// UpdateStatusAsync's outcome DOES need an enum: OrderNotFound (404) and
// InvalidTransition (400) are different responses, same shape as LinkResult.
public enum OrderStatusUpdateResult
{
    Success,
    OrderNotFound,
    InvalidTransition   // covers: backwards/skipped transitions, and manually setting Paid (Stripe-webhook-only)
}

public interface IOrderService
{
    Task<List<OrderDto>> GetAllAsync();
    Task<OrderDto?> GetByIdAsync(int id);
    Task<OrderCreateResult> CreateAsync(OrderCreateDto dto);
    Task<OrderStatusUpdateResult> UpdateStatusAsync(int id, OrderStatusUpdateDto dto);

    // No DeleteAsync: orders are never deleted, only moved to Cancelled via UpdateStatusAsync
    // (matches the "no OrderItemUpdateDto" call in DTOs/OrderItemDto.cs
    // wrong orders get cancelled/recreated by staff, not edited or removed
}
