using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Tests for OrderService.MarkOrderPaidAsync -> the state change the Stripe webhook drives.
// The idempotency test : Stripe can deliver the same event more than once,
// so processing it twice must NOT do anything the second time.
public class OrderPaymentTests
{
    // A fresh, isolated in-memory database per test (unique name), so tests never bleed into each other.
    // MarkOrderPaidAsync only touches the DbContext, so the other three constructor dependencies
    // (business-hours, Stripe and settings) are never called here and can safely be null.
    private static (OrderService service, AppDbContext context) BuildService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new OrderService(context, null!, null!, null!, null!);
        return (service, context);
    }

    private static Order SeedOrder(AppDbContext context, string paymentIntentId, OrderStatus status)
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

    [Fact]
    public async Task First_delivery_moves_a_Pending_order_to_Paid()
    {
        var (service, context) = BuildService();
        var order = SeedOrder(context, "pi_123", OrderStatus.Pending);

        var result = await service.MarkOrderPaidAsync("pi_123");

        Assert.Equal(OrderPaymentResult.MarkedPaid, result);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task Duplicate_delivery_of_the_same_event_is_a_no_op()
    {
        var (service, context) = BuildService();
        var order = SeedOrder(context, "pi_123", OrderStatus.Pending);

        var first = await service.MarkOrderPaidAsync("pi_123");   // real payment
        var second = await service.MarkOrderPaidAsync("pi_123");  // Stripe re-delivering the same event

        Assert.Equal(OrderPaymentResult.MarkedPaid, first);
        Assert.Equal(OrderPaymentResult.AlreadyPaid, second); // idempotent: the second call changes nothing
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task Unknown_payment_intent_id_returns_OrderNotFound()
    {
        var (service, context) = BuildService();
        SeedOrder(context, "pi_123", OrderStatus.Pending);

        var result = await service.MarkOrderPaidAsync("pi_does_not_exist");

        Assert.Equal(OrderPaymentResult.OrderNotFound, result);
    }

    [Fact]
    public async Task A_cancelled_order_is_not_resurrected_to_Paid()
    {
        var (service, context) = BuildService();
        var order = SeedOrder(context, "pi_123", OrderStatus.Cancelled);

        var result = await service.MarkOrderPaidAsync("pi_123");

        Assert.Equal(OrderPaymentResult.NotPayable, result);
        Assert.Equal(OrderStatus.Cancelled, order.Status); // status untouched
    }
}
