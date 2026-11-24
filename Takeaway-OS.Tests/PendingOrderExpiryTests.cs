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
}
