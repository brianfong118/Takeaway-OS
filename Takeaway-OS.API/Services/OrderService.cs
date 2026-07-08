using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    public OrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<OrderDto>> GetAllAsync()
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemModifiers)
            .OrderByDescending(o => o.CreatedAt) // newest first — most useful default for staff scanning the list
            .ToListAsync();
        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto?> GetByIdAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemModifiers)
            .FirstOrDefaultAsync(o => o.Id == id); 

        return order is null ? null : MapToDto(order);
    }

    public async Task<OrderCreateResult> CreateAsync(OrderCreateDto dto)
    {
        // NOTE: no business-hours check here yet — deliberately stubbed as "always open" for this
        // first pass. Revisit once OpeningHours exists.

        if (dto.Items.Count == 0)
            return new OrderCreateResult { Error = "Order must contain at least one item." };

        var order = new Order
        {
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            DeliveryAddress = dto.DeliveryAddress,
            OrderType = dto.OrderType,
            Notes = dto.Notes,
            Status = OrderStatus.Pending, // every new order starts here cuz client can't choose a starting status
            CreatedAt = DateTime.UtcNow
        };

        foreach (var itemDto in dto.Items)
        {
            // Need the MenuItem's linked ModifierGroups (and each group's Options) to know
            // which groups this item actually offers + each group's Min/Max/IsRequired rules.
            var menuItem = await _context.MenuItems
                .Include(m => m.MenuItemModifierGroups)
                    .ThenInclude(link => link.ModifierGroup)
                .FirstOrDefaultAsync(m => m.Id == itemDto.MenuItemId);

            if (menuItem is null)
                return new OrderCreateResult { Error = $"MenuItem {itemDto.MenuItemId} does not exist." };

            // Validate + collect every submitted option up front so we can snapshot Name/PriceDelta
            // later without hitting the database again.
            var selectedOptions = new List<ModifierOption>();
            var offeredGroupIds = menuItem.MenuItemModifierGroups.Select(link => link.ModifierGroupId).ToHashSet();

            foreach (var optionId in itemDto.ModifierOptionIds)
            {
                var option = await _context.ModifierOptions.FindAsync(optionId);
                if (option is null)
                    return new OrderCreateResult { Error = $"ModifierOption {optionId} does not exist." };
                if (!offeredGroupIds.Contains(option.ModifierGroupId))
                    return new OrderCreateResult { Error = $"ModifierOption {optionId} ('{option.Name}') is not offered on MenuItem {itemDto.MenuItemId}." };

                selectedOptions.Add(option);
            }

            // Check every group this item offers against what was actually selected —
            // not just the groups the customer happened to touch.
            foreach (var link in menuItem.MenuItemModifierGroups)
            {
                var group = link.ModifierGroup;
                var selectedCount = selectedOptions.Count(o => o.ModifierGroupId == group.Id);

                // IsRequired and MinSelect are set independently when the group is configured,
                // so they could disagree (e.g. IsRequired = true but MinSelect left at 0).
                // Effective minimum takes the stricter of the two.
                var effectiveMin = group.IsRequired ? Math.Max(group.MinSelect, 1) : group.MinSelect;

                if (selectedCount < effectiveMin)
                    return new OrderCreateResult { Error = $"'{group.Name}' requires at least {effectiveMin} selection(s) for MenuItem {itemDto.MenuItemId}." };

                if (selectedCount > group.MaxSelect)
                    return new OrderCreateResult { Error = $"'{group.Name}' allows at most {group.MaxSelect} selection(s) for MenuItem {itemDto.MenuItemId}." };
            }

            var orderItem = new OrderItem
            {
                ItemName = menuItem.Name,    // snapshot -> copied now, never looked up again after this
                UnitPrice = menuItem.Price,  // snapshot
                Quantity = itemDto.Quantity,
                Notes = itemDto.Notes
            };

            foreach (var option in selectedOptions)
            {
                orderItem.OrderItemModifiers.Add(new OrderItemModifier
                {
                    Name = option.Name,            // snapshot
                    PriceDelta = option.PriceDelta  // snapshot
                });
            }

            order.OrderItems.Add(orderItem);
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(); // one call persists Order + every OrderItem + every OrderItemModifier together, via the object graph built above

        return new OrderCreateResult { Order = MapToDto(order) };
    }

    public async Task<OrderStatusUpdateResult> UpdateStatusAsync(int id, OrderStatusUpdateDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order is null) return OrderStatusUpdateResult.OrderNotFound;

        if (!IsValidTransition(order, dto.Status))
            return OrderStatusUpdateResult.InvalidTransition;

        order.Status = dto.Status;
        await _context.SaveChangesAsync();
        return OrderStatusUpdateResult.Success;
    }

    // Forward flow only: Pending → Paid → Preparing → Ready → (OutForDelivery →) Completed.
    // Paid is rejected unconditionally -> only ever set by the future Stripe webhook NOT through this endpoint
    // OutForDelivery only makes sense for delivery orders 
    // Pickup orders go straight from Ready to Completed. 
    // Preparing has no path to Cancelled yet -> needs a staff-override flow that isn't built yet.
    private static bool IsValidTransition(Order order, OrderStatus newStatus)
    {
        if (newStatus == OrderStatus.Paid) return false;

        return (order.Status, newStatus) switch
        {
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.Paid, OrderStatus.Preparing) => true,
            (OrderStatus.Paid, OrderStatus.Cancelled) => true,
            (OrderStatus.Preparing, OrderStatus.Ready) => true,
            (OrderStatus.Ready, OrderStatus.OutForDelivery) => order.OrderType == OrderType.Delivery,
            (OrderStatus.Ready, OrderStatus.Completed) => order.OrderType == OrderType.Collection,
            (OrderStatus.OutForDelivery, OrderStatus.Completed) => true,
            _ => false // Completed and Cancelled are terminal
        };
    }

    private static OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            DeliveryAddress = order.DeliveryAddress,
            OrderType = order.OrderType,
            Status = order.Status,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            Items = order.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                ItemName = oi.ItemName,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity,
                Notes = oi.Notes,
                Modifiers = oi.OrderItemModifiers.Select(m => new OrderItemModifierDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    PriceDelta = m.PriceDelta
                }).ToList()
            }).ToList(),
            // Never stored — always derived fresh from the line items so it can't drift from them.
            Total = order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity)
                  + order.OrderItems.Sum(oi => oi.OrderItemModifiers.Sum(m => m.PriceDelta))
        };
    }
}
