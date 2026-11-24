using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Unit tests for the pricing formula in OrderService.ComputeTotal:
//   sum over lines of (UnitPrice + that line's modifier PriceDeltas) * Quantity  +  DeliveryFee
// It runs once, at order creation, and the result is stored on Order.Total; these tests pin the
// formula down, and the last one pins down that reads use the stored value instead of recomputing.
public class OrderTotalTests
{
    [Fact]
    public void Total_sums_each_line_price_plus_its_modifiers_times_quantity()
    {
        // Arrange: two lines. First is £5.00 x2 with a +£0.50 modifier; second is £3.00 x1 with none.
        var order = new Order
        {
            OrderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    UnitPrice = 5.00m, // 'm' suffix makes this a decimal literal -> money is ALWAYS decimal,
                    Quantity = 2,      // never double/float, or totals drift by rounding (e.g. 0.1 + 0.2 != 0.3 in float)
                    OrderItemModifiers = new List<OrderItemModifier> { new() { PriceDelta = 0.50m } }
                },
                new OrderItem { UnitPrice = 3.00m, Quantity = 1 }
            }
        };

        var total = OrderService.ComputeTotal(order);

        Assert.Equal(14.00m, total); // (5.00 + 0.50) * 2 + 3.00 * 1 = 14.00
    }

    [Fact]
    public void Modifiers_are_charged_once_per_portion()
    {
        // The case that motivated per-portion pricing: three pizzas, each made with extra cheese.
        // Charging the cheese once (the old per-line formula) would give 25.00 for food worth 27.00.
        var order = new Order
        {
            OrderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    UnitPrice = 8.00m,
                    Quantity = 3,
                    OrderItemModifiers = new List<OrderItemModifier> { new() { PriceDelta = 1.00m } }
                }
            }
        };

        Assert.Equal(27.00m, OrderService.ComputeTotal(order)); // (8.00 + 1.00) * 3
    }

    [Fact]
    public void Modifier_with_zero_delta_does_not_change_total()
    {
        // A "remove X" style option is priced at 0 — it must not affect the total.
        var order = new Order
        {
            OrderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    UnitPrice = 4.00m,
                    Quantity = 1,
                    OrderItemModifiers = new List<OrderItemModifier> { new() { PriceDelta = 0m } }
                }
            }
        };

        Assert.Equal(4.00m, OrderService.ComputeTotal(order));
    }

    [Fact]
    public void Order_with_no_items_totals_zero()
    {
        var order = new Order(); // OrderItems defaults to an empty list

        Assert.Equal(0m, OrderService.ComputeTotal(order));
    }

    [Fact]
    public void Delivery_fee_is_added_once_per_order_not_once_per_line()
    {
        // Three lines on purpose: a fee folded into the per-line calculation would be
        // counted three times, which is the exact mistake this pins down.
        var order = new Order
        {
            DeliveryFee = 2.50m,
            OrderItems = new List<OrderItem>
            {
                new OrderItem { UnitPrice = 5.00m, Quantity = 1 },
                new OrderItem { UnitPrice = 3.00m, Quantity = 2 },
                new OrderItem { UnitPrice = 1.00m, Quantity = 1 }
            }
        };

        Assert.Equal(14.50m, OrderService.ComputeTotal(order)); // 5.00 + 6.00 + 1.00 + 2.50 once = 14.50
        Assert.Equal(2.50m, OrderService.MapToDto(order).DeliveryFee); // surfaced separately so the client can show a breakdown
    }

    [Fact]
    public void A_zero_delivery_fee_leaves_the_total_untouched()
    {
        // What every collection order looks like: the OrderType branch happens at creation,
        // so by the time the total is computed a collection order is just a fee of 0.
        var order = new Order
        {
            DeliveryFee = 0m,
            OrderItems = new List<OrderItem> { new OrderItem { UnitPrice = 8.00m, Quantity = 1 } }
        };

        Assert.Equal(8.00m, OrderService.ComputeTotal(order));
    }

    [Fact]
    public void Reads_show_the_stored_total_not_a_recalculation()
    {
        // An order whose stored Total differs from what today's formula would give, as a pre-change
        // order would after the pricing formula moves on. The DTOs must show what was CHARGED.
        var order = new Order
        {
            Total = 10.00m,
            OrderItems = new List<OrderItem> { new OrderItem { UnitPrice = 12.00m, Quantity = 1 } }
        };

        Assert.Equal(10.00m, OrderService.MapToDto(order).Total);
        Assert.Equal(10.00m, OrderService.MapToGuestDto(order).Total);
    }
}
