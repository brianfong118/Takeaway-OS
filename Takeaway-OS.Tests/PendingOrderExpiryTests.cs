using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Tests for OrderService.ExpireStalePendingOrdersAsync, the work PendingOrderExpiryService runs on
// a timer. Tested on the service rather than through the timer: the timer is plumbing, and the rules
// about which orders may be cancelled are what actually protect the customer's money.
public class PendingOrderExpiryTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly DateTime Cutoff = Now - PendingOrderExpiryService.UnpaidTimeout;

    private static (OrderService service, AppDbContext context, FakeStripeService stripe) BuildService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var stripe = new FakeStripeService();
        var service = new OrderService(context, null!, stripe, null!, null!);
        return (service, context, stripe);
    }

    private static Order SeedOrder(AppDbContext context, OrderStatus status, TimeSpan age, string? paymentIntentId = "pi_123")
    {
        var order = new Order
        {
            CustomerName = "Test",
            CustomerPhone = "0000",
            Status = status,
            StripePaymentIntentId = paymentIntentId,
            CreatedAt = Now - age
        };
        context.Orders.Add(order);
        context.SaveChanges();
        return order;
    }

    [Fact]
    public async Task An_order_left_unpaid_past_the_timeout_is_cancelled_and_its_payment_voided()
    {
        var (service, context, stripe) = BuildService();
        var stale = SeedOrder(context, OrderStatus.Pending, TimeSpan.FromMinutes(31));

        var result = await service.ExpireStalePendingOrdersAsync(Cutoff);

        Assert.Equal(1, result.Expired);
        Assert.Equal(OrderStatus.Cancelled, stale.Status);
        Assert.Equal(new[] { "pi_123" }, stripe.CancelledPaymentIntents);
    }

    [Fact]
    public async Task Recent_pending_orders_and_paid_orders_are_left_alone()
    {
        var (service, context, stripe) = BuildService();
        var fresh = SeedOrder(context, OrderStatus.Pending, TimeSpan.FromMinutes(5)); // still checking out
        var paid = SeedOrder(context, OrderStatus.Paid, TimeSpan.FromHours(2));       // old, but paid

        var result = await service.ExpireStalePendingOrdersAsync(Cutoff);

        Assert.Equal(0, result.Expired);
        Assert.Equal(OrderStatus.Pending, fresh.Status);
        Assert.Equal(OrderStatus.Paid, paid.Status);
        Assert.Empty(stripe.CancelledPaymentIntents);
    }

    [Fact]
    public async Task A_stale_order_that_turns_out_to_be_paid_is_left_for_the_webhook()
    {
        var (service, context, stripe) = BuildService();
        stripe.PaymentAlreadyTaken = true;
        var order = SeedOrder(context, OrderStatus.Pending, TimeSpan.FromMinutes(45));

        var result = await service.ExpireStalePendingOrdersAsync(Cutoff);

        Assert.Equal(1, result.LeftForWebhook);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task A_stripe_failure_on_one_order_does_not_stop_the_rest()
    {
        var (service, context, stripe) = BuildService();
        stripe.FailCancels = true;
        var unreachable = SeedOrder(context, OrderStatus.Pending, TimeSpan.FromMinutes(50));
        // No PaymentIntent, so it needs no Stripe call and can still be expired this pass.
        var noPayment = SeedOrder(context, OrderStatus.Pending, TimeSpan.FromMinutes(40), paymentIntentId: null);

        var result = await service.ExpireStalePendingOrdersAsync(Cutoff);

        Assert.Equal(new[] { unreachable.Id }, result.FailedOrderIds);
        Assert.Equal(OrderStatus.Pending, unreachable.Status); // retried next pass
        Assert.Equal(OrderStatus.Cancelled, noPayment.Status);
        Assert.Equal(1, result.Expired);
    }

    [Fact]
    public async Task Orders_that_keep_failing_do_not_block_newer_ones()
    {
        // The trap the old "oldest 50" query fell into: 60 old orders that fail every time (e.g.
        // PaymentIntents from a previous Stripe key) stayed the oldest 50, so a newer abandoned order
        // was never reached. More than one batch's worth on purpose.
        var (service, context, stripe) = BuildService();
        for (var i = 0; i < 60; i++)
        {
            var pi = $"pi_stuck_{i}";
            stripe.FailCancelsFor.Add(pi);
            SeedOrder(context, OrderStatus.Pending, TimeSpan.FromHours(5), paymentIntentId: pi);
        }
        var newer = SeedOrder(context, OrderStatus.Pending, TimeSpan.FromMinutes(45), paymentIntentId: "pi_newer");

        var result = await service.ExpireStalePendingOrdersAsync(Cutoff);

        Assert.Equal(60, result.FailedOrderIds.Count);
        Assert.Equal(OrderStatus.Cancelled, newer.Status); // reached despite the 60 in front of it
    }

    [Fact]
    public async Task A_payment_made_long_after_the_timeout_is_refunded_not_accepted()
    {
        // The server was asleep, so no expiry pass voided this order; the customer paid 3 hours later.
        var (service, context, stripe) = BuildService();
        var order = SeedOrder(context, OrderStatus.Pending, TimeSpan.FromHours(3));

        var result = await service.MarkOrderPaidAsync("pi_123", paidAtUtc: Now);

        Assert.Equal(OrderPaymentResult.LatePaymentRefunded, result);
        Assert.Equal(OrderStatus.Cancelled, order.Status); // never reaches the kitchen
        Assert.Equal(new[] { "pi_123" }, stripe.RefundedPaymentIntents);
    }

    [Fact]
    public async Task A_payment_made_in_time_is_accepted_even_if_the_webhook_arrives_late()
    {
        // Paid 10 minutes after ordering, but the webhook only arrives now, 2 hours later (e.g. it had
        // to wake the server, or Stripe retried). What counts is when Stripe recorded the payment.
        var (service, context, stripe) = BuildService();
        var order = SeedOrder(context, OrderStatus.Pending, TimeSpan.FromHours(2));
        var paidAt = order.CreatedAt + TimeSpan.FromMinutes(10);

        var result = await service.MarkOrderPaidAsync("pi_123", paidAt);

        Assert.Equal(OrderPaymentResult.MarkedPaid, result);
        Assert.Equal(paidAt, order.PaidAt); // Stripe's time, not the webhook's arrival time
        Assert.Empty(stripe.RefundedPaymentIntents);
    }
}
