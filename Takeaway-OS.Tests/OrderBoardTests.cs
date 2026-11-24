using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Tests for OrderService.GetBoardAsync, the query behind the owner dashboard's 10-second poll.
// It must include everything the kitchen still has to act on, and stay bounded otherwise.
public class OrderBoardTests
{
    [Fact]
    public async Task Board_shows_live_orders_and_the_last_day_of_history_but_never_unpaid_ones()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        var service = new OrderService(context, null!, null!, null!, null!);

        var now = DateTime.UtcNow;
        Order Seed(OrderStatus status, TimeSpan age)
        {
            var order = new Order { CustomerName = status.ToString(), Status = status, CreatedAt = now - age };
            context.Orders.Add(order);
            return order;
        }

        var paid = Seed(OrderStatus.Paid, TimeSpan.FromMinutes(5));
        var staleButLive = Seed(OrderStatus.OutForDelivery, TimeSpan.FromDays(3)); // still needs dealing with
        var recentlyCompleted = Seed(OrderStatus.Completed, TimeSpan.FromHours(3));
        Seed(OrderStatus.Pending, TimeSpan.FromMinutes(2));      // unpaid: the kitchen never sees it
        Seed(OrderStatus.Completed, TimeSpan.FromDays(2));       // history older than 24h
        Seed(OrderStatus.Cancelled, TimeSpan.FromHours(30));     // likewise
        await context.SaveChangesAsync();

        var board = await service.GetBoardAsync();

        var ids = board.Select(o => o.Id).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { paid.Id, staleButLive.Id, recentlyCompleted.Id }.OrderBy(id => id).ToArray(), ids);
    }
}
