using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Unit tests for the driver's status-transition rules (OrderService.IsValidDriverTransition)
public class OrderStatusTransitionTests
{
    [Fact]
    public void Driver_can_move_Ready_to_OutForDelivery()
    {
        var order = new Order { Status = OrderStatus.Ready, OrderType = OrderType.Delivery };
        var allowed = OrderService.IsValidDriverTransition(order, OrderStatus.OutForDelivery);
        Assert.True(allowed);
    }

    [Fact]
    public void Driver_cannot_skip_Ready_straight_to_Completed()
    {
        var order = new Order { Status = OrderStatus.Ready, OrderType = OrderType.Delivery };
        var allowed = OrderService.IsValidDriverTransition(order, OrderStatus.Completed);
        Assert.False(allowed);
    }

    [Theory]
    [InlineData(OrderStatus.Ready,          OrderStatus.OutForDelivery, true)]   // pick the order up
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Completed,      true)]   // drop it off
    [InlineData(OrderStatus.Ready,          OrderStatus.Completed,      false)]  // that's the collection path (owner), not a driver's
    [InlineData(OrderStatus.Pending,        OrderStatus.OutForDelivery, false)]  // can't start a delivery on an unprepared order
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Ready,          false)]  // no going backwards
    [InlineData(OrderStatus.Completed,      OrderStatus.OutForDelivery, false)]  // Completed is terminal
    [InlineData(OrderStatus.Ready,          OrderStatus.Cancelled,      false)]  // cancelling is not a driver power
    public void Driver_transition_matrix(OrderStatus from, OrderStatus to, bool expected)
    {
        var order = new Order { Status = from, OrderType = OrderType.Delivery };
        var allowed = OrderService.IsValidDriverTransition(order, to);

        // Assert.Equal(expected, actual): the convention is expected-first, so the failure message reads right.
        Assert.Equal(expected, allowed);
    }

    [Theory]
    [InlineData(OrderStatus.Pending,        OrderStatus.Cancelled,      OrderType.Delivery,   true)]  // cancel while still pending
    [InlineData(OrderStatus.Paid,           OrderStatus.Preparing,      OrderType.Delivery,   true)]  // kitchen starts
    [InlineData(OrderStatus.Paid,           OrderStatus.Cancelled,      OrderType.Delivery,   true)]  // refund/cancel a paid order
    [InlineData(OrderStatus.Preparing,      OrderStatus.Ready,          OrderType.Delivery,   true)]
    [InlineData(OrderStatus.Ready,          OrderStatus.OutForDelivery, OrderType.Delivery,   true)]
    [InlineData(OrderStatus.OutForDelivery, OrderStatus.Completed,      OrderType.Delivery,   true)]
    [InlineData(OrderStatus.Ready,          OrderStatus.Completed,      OrderType.Collection, true)]  // collection finishes here
    [InlineData(OrderStatus.Ready,          OrderStatus.OutForDelivery, OrderType.Collection, false)] // no driver leg for collection
    [InlineData(OrderStatus.Ready,          OrderStatus.Completed,      OrderType.Delivery,   false)] // delivery must go via OutForDelivery
    [InlineData(OrderStatus.Pending,        OrderStatus.Paid,           OrderType.Delivery,   false)] // Paid is webhook-only, never this path
    [InlineData(OrderStatus.Preparing,      OrderStatus.Cancelled,      OrderType.Delivery,   false)] // kitchen started -> needs staff override
    [InlineData(OrderStatus.Completed,      OrderStatus.Preparing,      OrderType.Delivery,   false)] // Completed is terminal
    public void Owner_transition_matrix(OrderStatus from, OrderStatus to, OrderType type, bool expected)
    {
        var order = new Order { Status = from, OrderType = type };
        var allowed = OrderService.IsValidTransition(order, to);
        Assert.Equal(expected, allowed);
    }
}
