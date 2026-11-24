using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IBusinessHoursService _businessHoursService;
    private readonly IStripeService _stripeService;
    private readonly IRestaurantSettingsService _settingsService;
    private readonly IDeliveryAreaService _deliveryAreaService;

    public OrderService(
        AppDbContext context,
        IBusinessHoursService businessHoursService,
        IStripeService stripeService,
        IRestaurantSettingsService settingsService,
        IDeliveryAreaService deliveryAreaService)
    {
        _context = context;
        _businessHoursService = businessHoursService;
        _stripeService = stripeService;
        _settingsService = settingsService;
        _deliveryAreaService = deliveryAreaService;
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

    // The JWT identifies an ApplicationUser (the login); orders are keyed by Customer.Id (the profile).
    // Every customer-scoped read goes through here so the mapping is done in exactly one place -
    // and so no caller can ever pass a Customer.Id straight in from a request 
    // (dont trust the client to tell us which customer they are)
    private async Task<int?> ResolveCustomerIdAsync(int applicationUserId)
    {
        return await _context.Customers
            .Where(c => c.ApplicationUserId == applicationUserId)
            .Select(c => (int?)c.Id) // nullable so the whole query returns null when no row matches, instead of throwing
            .FirstOrDefaultAsync();
    }

    private async Task<int?> ResolveDriverIdAsync(int applicationUserId)
    {
        return await _context.Drivers
            .Where(d => d.ApplicationUserId == applicationUserId)
            .Select(d => (int?)d.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<OrderDto>> GetForCustomerAsync(int applicationUserId)
    {
        var customerId = await ResolveCustomerIdAsync(applicationUserId);

        // No Customer profile (an Owner or Driver login hitting this) -> no history, not an error.
        if (customerId is null) return new List<OrderDto>();

        var orders = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemModifiers)
            .Where(o => o.CustomerId == customerId) // the scoping that makes this safe to expose to a non-Owner
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderDto?> GetByIdForCustomerAsync(int id, int applicationUserId)
    {
        var customerId = await ResolveCustomerIdAsync(applicationUserId);
        if (customerId is null) return null;

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemModifiers)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == customerId); // the scoping that makes this safe to expose to a non-Owner

        return order is null ? null : MapToDto(order);
    }

    // The guest confirmation lookup. No ownership filter, because a guest order has no owner to
    // match against -> possession of the token IS the check, and the unique index on PublicToken is
    // what makes "at most one order" a database guarantee rather than an assumption.
    public async Task<GuestOrderDto?> GetByTokenAsync(Guid token)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemModifiers) // needed for both the receipt lines and ComputeTotal
            .FirstOrDefaultAsync(o => o.PublicToken == token);

        return order is null ? null : MapToGuestDto(order);
    }

    public async Task<List<OrderDto>> GetAssignedAsync(int applicationUserId)
    {
        var driverId = await ResolveDriverIdAsync(applicationUserId);

        if (driverId is null) return new List<OrderDto>();

        var orders = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemModifiers)
            .Where(o => o.DriverId == driverId) // the scoping that keeps a driver to their own deliveries
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderStatusUpdateResult> UpdateStatusByDriverAsync(int id, int applicationUserId, OrderStatusUpdateDto dto)
    {
        var driverId = await ResolveDriverIdAsync(applicationUserId);
        if (driverId is null) return OrderStatusUpdateResult.OrderNotFound; // no profile -> no order is "theirs"

        // The DriverId filter is the whole security boundary: driver can't discover or touch others' orders.
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.DriverId == driverId);
        if (order is null) return OrderStatusUpdateResult.OrderNotFound;

        if (!IsValidDriverTransition(order, dto.Status))
            return OrderStatusUpdateResult.InvalidTransition;

        order.Status = dto.Status;
        await _context.SaveChangesAsync();
        return OrderStatusUpdateResult.Success;
    }

    public async Task<OrderCreateResult> CreateAsync(OrderCreateDto dto, int? applicationUserId)
    {
        var status = await _businessHoursService.GetStatusAsync();
        if (!status.IsOpen)
            return new OrderCreateResult { Error = status.Message, RestaurantClosed = true }; // 409, not 400 — see OrderCreateResult

        // Empty-basket, quantity, required-field and conditional-address checks now live on OrderCreateDto
        // (DataAnnotations + IValidatableObject); [ApiController] rejects those with a 400 before we get here.
        // All rules below need the database -> they can't be expressed as attributes.

        // A logged-in Owner/Driver has no Customer profile, so this stays null and their order is a guest order
        int? customerId = applicationUserId is null
            ? null
            : await ResolveCustomerIdAsync(applicationUserId.Value);

        // Default: whatever free text the request carried (guests, and customers who typed a one-off address).
        var deliveryAddress = dto.DeliveryAddress;
        var deliveryPostcode = dto.DeliveryPostcode;

        // A customer picked one of their saved addresses instead of retyping it -> SNAPSHOT
        if (dto.AddressId is not null)
        {
            if (customerId is null)
                return new OrderCreateResult { Error = "A saved address can only be used by a signed-in customer account." };

            // Scoped to the caller's own customerId: someone else's AddressId is indistinguishable from a null
            // customer can't probe another customer's address book through checkout.
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Id == dto.AddressId && a.CustomerId == customerId);
            if (address is null)
                return new OrderCreateResult { Error = $"Address {dto.AddressId} does not exist." };

            deliveryAddress = FormatAddress(address);

            // The saved address's OWN postcode wins over anything the request sent. Same
            // reasoning as CustomerId coming from the JWT: when a trusted server-side record
            // exists, the client's copy of it is not consulted.
            deliveryPostcode = address.Postcode;
        }

        // DELIVERY RADIUS. 
        // Placed here, before the fee snapshot and long before the Stripe call, so a refused
        // order never creates a PaymentIntent it would then have to abandon.
        var formattedPostcode = string.Empty;
        if (dto.OrderType == OrderType.Delivery)
        {
            // Unparseable is a REFUSAL, not a pass. 
            // If it can't be parsed the radius check can't run, and
            // "couldn't check" must not silently become "allowed".
            if (!UkPostcode.TryParse(deliveryPostcode, out var outwardCode, out formattedPostcode))
                return new OrderCreateResult { Error = $"'{deliveryPostcode}' is not a valid UK postcode." };

            // A plain 400 via Error, NOT the 409 the closed shop uses.
            if (!await _deliveryAreaService.IsOutwardCodeAllowedAsync(outwardCode))
                return new OrderCreateResult
                {
                    Error = $"Sorry, we don't deliver to {outwardCode}. You're welcome to place a collection order instead."
                };

            // Free-text address only: FormatAddress already put the postcode on the end for a saved address
            if (dto.AddressId is null)
                deliveryAddress = $"{deliveryAddress}, {formattedPostcode}";
        }

        // SNAPSHOT the fee that applies right now, exactly like OrderItem.UnitPrice below
        // Collection records 0, which is what lets ComputeTotal add the column with no branch.
        var settings = await _settingsService.GetAsync();
        var deliveryFee = dto.OrderType == OrderType.Delivery ? settings.DeliveryFee : 0m;

        var order = new Order
        {
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            DeliveryAddress = deliveryAddress,
            DeliveryPostcode = formattedPostcode, // "" on collection, by the branch above
            DeliveryFee = deliveryFee,
            CustomerId = customerId, // null = guest; never read from dto, only from the validated JWT
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

            if (!menuItem.IsAvailable)
                return new OrderCreateResult { Error = $"MenuItem {itemDto.MenuItemId} ('{menuItem.Name}') is not available." };

            // Validate + collect every submitted option up front so we can snapshot Name/PriceDelta
            // later without hitting the database again.
            var selectedOptions = new List<ModifierOption>();
            var offeredGroupIds = menuItem.MenuItemModifierGroups.Select(link => link.ModifierGroupId).ToHashSet();

            // The same option twice on one line ([7, 7]) is refused rather than silently collapsed.
            // The picker can't produce it (each option is a single checkbox or radio), so a repeat
            // means a hand-crafted or buggy request, and accepting it would snapshot and CHARGE
            // that option once per copy. Refusing is also clearer than guessing what was meant.
            if (itemDto.ModifierOptionIds.Distinct().Count() != itemDto.ModifierOptionIds.Count)
                return new OrderCreateResult { Error = $"A modifier was selected more than once for MenuItem {itemDto.MenuItemId}." };

            foreach (var optionId in itemDto.ModifierOptionIds)
            {
                var option = await _context.ModifierOptions.FindAsync(optionId);
                if (option is null)
                    return new OrderCreateResult { Error = $"ModifierOption {optionId} does not exist." };
                if (!option.IsActive)
                    return new OrderCreateResult { Error = $"ModifierOption {optionId} ('{option.Name}') is not available." };
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

        // Computed from the in-memory graph BEFORE saving, so the minimum check below can refuse
        // the order without ever writing it.
        var total = ComputeTotal(order);

        // Stripe rejects any GBP charge under its minimum. Without this check such an order would be
        // saved, then the PaymentIntent call would throw, leaving a 500 and an orphaned Pending order
        // for a basket that was never payable. A plain 400: the customer can add something.
        if (total < IStripeService.MinimumCharge)
            // "£" written out with a fixed "0.00" format rather than the "C" currency format, which
            // follows the SERVER's culture: in Render's container that is the invariant culture,
            // and the customer would read "¤0.30".
            return new OrderCreateResult { Error = $"Orders must come to at least £{IStripeService.MinimumCharge:0.00}." };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(); // one call persists Order + every OrderItem + every OrderItemModifier together, via the object graph built above

        // Create the Stripe PaymentIntent from calced total + that Db generated Id,
        // save once more to store the PaymentIntentId (the webhook's lookup key).
        // If Stripe throws here (bad key / network), the exception propagates to a 500 and the order is left Pending with no payment
        var paymentIntent = await _stripeService.CreatePaymentIntentAsync(total, order.Id);
        order.StripePaymentIntentId = paymentIntent.PaymentIntentId;
        await _context.SaveChangesAsync();

        return new OrderCreateResult
        {
            Order = MapToDto(order),
            ClientSecret = paymentIntent.ClientSecret,
            // Read off the entity, which already carries it: the property initialiser on Order
            // generated the value before this order was ever inserted. Nothing to look up.
            PublicToken = order.PublicToken
        };
    }

    public async Task<OrderStatusUpdateResult> UpdateStatusAsync(int id, OrderStatusUpdateDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order is null) return OrderStatusUpdateResult.OrderNotFound;

        if (!IsValidTransition(order, dto.Status))
            return OrderStatusUpdateResult.InvalidTransition;

        // Cancelling has to reverse the money as well as the status, or a cancelled order would
        // leave the customer charged (Paid) or still able to pay (Pending).
        // The Stripe call happens BEFORE the status is saved: if it throws, the exception
        // propagates to a 500 and the order keeps its old status, so "Cancelled" in our database
        // always means the money really was handled. IsValidTransition only allows Cancelled
        // from Pending or Paid, so those are the only two cases here.
        if (dto.Status == OrderStatus.Cancelled)
        {
            if (order.Status == OrderStatus.Paid)
            {
                // A Paid order always has a PaymentIntent, except the seeded demo orders, which
                // never went through Stripe and so have nothing to refund.
                if (order.StripePaymentIntentId is not null)
                    await _stripeService.RefundAsync(order.StripePaymentIntentId, order.Id);
            }
            else if (!await VoidPendingPaymentAsync(order))
            {
                return OrderStatusUpdateResult.PaymentInProgress;
            }
        }

        order.Status = dto.Status;
        await _context.SaveChangesAsync();
        return OrderStatusUpdateResult.Success;
    }

    // Makes a Pending order's payment impossible to complete, so the order can safely be cancelled.
    // Returns false when the customer has already paid (the webhook just hasn't landed yet): the
    // caller must then leave the order alone, because it is about to become Paid.
    // Shared by the Owner's cancel and the unpaid-order expiry, which both need exactly this rule.
    private async Task<bool> VoidPendingPaymentAsync(Order order)
    {
        // No PaymentIntent (seeded demo orders, or a creation whose Stripe call failed): there is
        // nothing anyone could pay, so there is nothing to void.
        if (order.StripePaymentIntentId is null) return true;

        return await _stripeService.CancelPaymentIntentAsync(order.StripePaymentIntentId);
    }

    public async Task<DriverAssignmentResult> AssignDriverAsync(int id, OrderDriverAssignmentDto dto)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order is null) return DriverAssignmentResult.OrderNotFound;

        if (order.OrderType != OrderType.Delivery)
            return DriverAssignmentResult.NotDeliveryOrder;

        // Only while the order is live. Pending isn't paid for yet, and Completed/Cancelled are
        // history: changing who delivered a finished order would rewrite the record, not help anyone.
        // Paid stays allowed even though the dashboard only offers the dropdown from Preparing on;
        // that is a screen-space choice, not a rule the server needs to enforce.
        if (order.Status is OrderStatus.Pending or OrderStatus.Completed or OrderStatus.Cancelled)
            return DriverAssignmentResult.OrderNotActive;

        if (dto.DriverId is not null)
        {
            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == dto.DriverId);
            if (!driverExists) return DriverAssignmentResult.DriverNotFound;
        }

        // Covers both directions: a real id assigns, null (owner reassigns) clears 
        order.DriverId = dto.DriverId;
        await _context.SaveChangesAsync();
        return DriverAssignmentResult.Success;
    }

    // Forward flow only: Pending → Paid → Preparing → Ready → (OutForDelivery →) Completed.
    // Paid is rejected unconditionally -> only ever set by the future Stripe webhook NOT through this endpoint
    // OutForDelivery only makes sense for delivery orders 
    // Pickup orders go straight from Ready to Completed. 
    // Preparing has no path to Cancelled yet -> needs a staff-override flow that isn't built yet.
    internal static bool IsValidTransition(Order order, OrderStatus newStatus)
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

    // WHITELIST, not a subset check against IsValidTransition: the driver's authority is defined by exactly these two pairs, 
    // anything a driver could otherwise reach is absent by construction rather than by extra guards.
    // No OrderType check needed: (AssignDriverAsync enforces that) + OrderType is never editable after creation.
    internal static bool IsValidDriverTransition(Order order, OrderStatus newStatus) 
    {
        return (order.Status, newStatus) switch
        {
            (OrderStatus.Ready, OrderStatus.OutForDelivery) => true,
            (OrderStatus.OutForDelivery, OrderStatus.Completed) => true,
            _ => false
        };
    }

    // Converts a saved Address -> single free-text line the Order snapshots and the driver reads.
    // Label ("Home"/"Work") is the customer's own nickname, not part of the postal address, so it's left out.
    // Empty lines (e.g. no Line2) are dropped so the result never has ", ," gaps.
    internal static string FormatAddress(Address address) 
    {
        var parts = new[] { address.Line1, address.Line2, address.City, address.Postcode }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join(", ", parts);
    }

    internal static OrderDto MapToDto(Order order) 
    {
        return new OrderDto
        {
            Id = order.Id,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            DeliveryAddress = order.DeliveryAddress,
            DeliveryPostcode = order.DeliveryPostcode, // the snapshot, like DeliveryFee below
            OrderType = order.OrderType,
            Status = order.Status,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            CustomerId = order.CustomerId,
            DriverId = order.DriverId,
            Items = MapItems(order),
            DeliveryFee = order.DeliveryFee, // the snapshot, read straight off the row
            // Never stored — always derived fresh from the line items so it can't drift from them.
            Total = ComputeTotal(order)
        };
    }

    // The guest confirmation projection. Every field OrderDto carries that isn't here is omitted
    // on purpose, not by oversight -> see GuestOrderDto for which, and why.
    internal static GuestOrderDto MapToGuestDto(Order order)
    {
        return new GuestOrderDto
        {
            Id = order.Id,
            OrderType = order.OrderType,
            Status = order.Status,
            DeliveryAddress = order.DeliveryAddress,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            Items = MapItems(order),
            DeliveryFee = order.DeliveryFee,
            // Same ComputeTotal as MapToDto, so the guest's receipt, the Owner's list and the amount
            // charged in Stripe are all one calculation and can't drift apart.
            Total = ComputeTotal(order)
        };
    }

    // Shared by both projections above: the line items are identical in each, and the customer's own
    // basket contents are not what either shape is being careful about.
    private static List<OrderItemDto> MapItems(Order order)
    {
        return order.OrderItems.Select(oi => new OrderItemDto
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
        }).ToList();
    }

    // The authoritative order total: sum(UnitPrice * Quantity) + sum(modifier PriceDeltas) + the delivery fee.
    // Extracted so the amount we charge in Stripe and the Total we show the client come from the
    // exact same calculation -> never disagree
    internal static decimal ComputeTotal(Order order)
    {
        return order.OrderItems.Sum(oi => oi.UnitPrice * oi.Quantity)
             + order.OrderItems.Sum(oi => oi.OrderItemModifiers.Sum(m => m.PriceDelta))
             // Per ORDER, not per line -> added once, outside both sums. Already 0 on a
             // collection order, so there's deliberately no OrderType check here.
             + order.DeliveryFee;
    }

    // The webhook's state change, kept here with every other Order status transition.
    // Idempotent by design
    // Deliberately bypasses IsValidTransition -> the one authorized path to Paid.
    public async Task<OrderPaymentResult> MarkOrderPaidAsync(string paymentIntentId)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.StripePaymentIntentId == paymentIntentId);

        if (order is null) return OrderPaymentResult.OrderNotFound;
        if (order.Status == OrderStatus.Paid) return OrderPaymentResult.AlreadyPaid; // duplicate event -> no-op
        if (order.Status != OrderStatus.Pending) return OrderPaymentResult.NotPayable; // e.g. Cancelled -> don't resurrect

        order.Status = OrderStatus.Paid;
        await _context.SaveChangesAsync();
        return OrderPaymentResult.MarkedPaid;
    }

    // At most this many orders per pass, oldest first, so one pass after a long outage can't make
    // hundreds of Stripe calls in a row. Anything left over is picked up by the next pass.
    private const int ExpiryBatchSize = 50;

    public async Task<PendingOrderExpiryResult> ExpireStalePendingOrdersAsync(DateTime cutoffUtc)
    {
        var result = new PendingOrderExpiryResult();

        var staleOrders = await _context.Orders
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoffUtc)
            .OrderBy(o => o.CreatedAt)
            .Take(ExpiryBatchSize)
            .ToListAsync();

        foreach (var order in staleOrders)
        {
            bool voided;
            try
            {
                // The same rule the Owner's cancel uses: make the payment impossible first, and only
                // then call the order Cancelled.
                voided = await VoidPendingPaymentAsync(order);
            }
            catch (Stripe.StripeException)
            {
                // One order's Stripe failure must not stop the rest of the batch. It stays Pending
                // and is simply retried on the next pass. Only Stripe errors are caught here: a
                // database error is a different kind of problem and goes up to the job's own handler.
                result.FailedOrderIds.Add(order.Id);
                continue;
            }

            if (!voided)
            {
                // The customer paid after all (the webhook is on its way), so this is about to be a
                // real Paid order. Leave it alone.
                result.LeftForWebhook++;
                continue;
            }

            order.Status = OrderStatus.Cancelled;

            // Saved per order rather than once at the end: each PaymentIntent is already cancelled in
            // Stripe by this point, so recording that immediately means a later failure in the loop
            // can't leave a voided payment attached to an order that still says Pending.
            await _context.SaveChangesAsync();
            result.Expired++;
        }

        return result;
    }
}
