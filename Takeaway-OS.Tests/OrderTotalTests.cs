using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Unit tests for the order Total computed in OrderService.MapToDto:
//   sum(UnitPrice * Quantity)  +  sum(every modifier's PriceDelta)
// Total is never stored; it's derived on every read so this test pins the formula down.
public class OrderTotalTests
{
    [Fact]
    public void Total_sums_line_prices_times_quantity_plus_modifier_deltas()
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

        var dto = OrderService.MapToDto(order);

        Assert.Equal(13.50m, dto.Total); // (5.00 * 2) + (3.00 * 1) + 0.50 = 13.50
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

        var dto = OrderService.MapToDto(order);

        Assert.Equal(4.00m, dto.Total);
    }

    [Fact]
    public void Order_with_no_items_totals_zero()
    {
        var order = new Order(); // OrderItems defaults to an empty list

        var dto = OrderService.MapToDto(order);

        Assert.Equal(0m, dto.Total);
    }
}
