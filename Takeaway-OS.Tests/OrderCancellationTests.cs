using Microsoft.EntityFrameworkCore;
using Stripe;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Cancelling an order must reverse the money as well as the status:
//   Paid    -> refund the charge, then mark Cancelled
//   Pending -> void the PaymentIntent so it can't be paid later, then mark Cancelled
// and if Stripe can't do its half, the status must NOT change.
public class OrderCancellationTests
{
    private static (OrderService service, AppDbContext context, FakeStripeService stripe) BuildService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var stripe = new FakeStripeService();

        // UpdateStatusAsync never touches business hours, settings or delivery areas, so those stay null.
        var service = new OrderService(context, null!, stripe, null!, null!);
        return (service, context, stripe);
    }

    private static Order SeedOrder(AppDbContext context, OrderStatus status, string? paymentIntentId = "pi_123")
    {
        var order = new Order
        {
            CustomerName = "Test",
            CustomerPhone = "0000",
            Status = status,
            StripePaymentIntentId = paymentIntentId
        };
        context.Orders.Add(order);
        context.SaveChanges();
        return order;
    }

    private static OrderStatusUpdateDto Cancel() => new() { Status = OrderStatus.Cancelled };

    [Fact]
    public async Task Cancelling_a_paid_order_refunds_it()
    {
        var (service, context, stripe) = BuildService();
        var order = SeedOrder(context, OrderStatus.Paid);

        var result = await service.UpdateStatusAsync(order.Id, Cancel());

        Assert.Equal(OrderStatusUpdateResult.Success, result);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(new[] { "pi_123" }, stripe.RefundedPaymentIntents);
    }

    [Fact]
    public async Task Cancelling_a_pending_order_voids_its_payment()
    {
        var (service, context, stripe) = BuildService();
        var order = SeedOrder(context, OrderStatus.Pending);

        var result = await service.UpdateStatusAsync(order.Id, Cancel());

        Assert.Equal(OrderStatusUpdateResult.Success, result);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(new[] { "pi_123" }, stripe.CancelledPaymentIntents);
        Assert.Empty(stripe.RefundedPaymentIntents); // nothing was charged, so nothing to refund
    }

    [Fact]
    public async Task Cancelling_a_pending_order_the_customer_just_paid_is_refused()
    {
        var (service, context, stripe) = BuildService();
        stripe.PaymentAlreadyTaken = true; // paid a moment ago; the webhook hasn't arrived yet
        var order = SeedOrder(context, OrderStatus.Pending);

        var result = await service.UpdateStatusAsync(order.Id, Cancel());

        Assert.Equal(OrderStatusUpdateResult.PaymentInProgress, result);
        Assert.Equal(OrderStatus.Pending, order.Status); // left for the webhook to mark Paid
    }

    [Fact]
    public async Task A_failed_refund_leaves_the_order_paid()
    {
        var (service, context, stripe) = BuildService();
        stripe.FailRefunds = true;
        var order = SeedOrder(context, OrderStatus.Paid);

        await Assert.ThrowsAsync<StripeException>(() => service.UpdateStatusAsync(order.Id, Cancel()));

        // The customer still has been charged, so the order must still say Paid.
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task An_order_without_a_payment_intent_is_cancelled_without_calling_stripe()
    {
        // The seeded demo orders never went through Stripe, so they carry no PaymentIntent id.
        var (service, context, stripe) = BuildService();
        var order = SeedOrder(context, OrderStatus.Paid, paymentIntentId: null);

        var result = await service.UpdateStatusAsync(order.Id, Cancel());

        Assert.Equal(OrderStatusUpdateResult.Success, result);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Empty(stripe.RefundedPaymentIntents);
        Assert.Empty(stripe.CancelledPaymentIntents);
    }
}
