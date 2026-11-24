using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Rules OrderService enforces when an order is created or a driver is assigned, which need the
// database and so can't be DataAnnotations on the DTO.
public class OrderCreationTests
{
    private readonly AppDbContext _context;
    private readonly FakeStripeService _stripe = new();
    private readonly OrderService _service;

    // Ids of the seeded menu, read back after SaveChanges assigns them.
    private readonly int _pizzaId;
    private readonly int _dipId;
    private readonly int _extraCheeseId;

    // xUnit builds a NEW instance of the test class for every [Fact], so this constructor is the
    // per-test setup: each test gets its own fresh in-memory database and menu.
    public OrderCreationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        var toppings = new ModifierGroup { Name = "Toppings", MinSelect = 0, MaxSelect = 3 };
        var extraCheese = new ModifierOption { Name = "Extra cheese", PriceDelta = 1.00m, ModifierGroup = toppings };
        var mains = new Category { Name = "Mains" };
        var pizza = new MenuItem { Name = "Pizza", Price = 8.00m, Category = mains };
        pizza.MenuItemModifierGroups.Add(new MenuItemModifierGroup { ModifierGroup = toppings });
        var dip = new MenuItem { Name = "Garlic dip", Price = 0.20m, Category = mains }; // under Stripe's 30p minimum on its own

        _context.AddRange(extraCheese, pizza, dip);
        _context.SaveChanges();

        _pizzaId = pizza.Id;
        _dipId = dip.Id;
        _extraCheeseId = extraCheese.Id;

        // Collection orders only, so the delivery-area service is never consulted and can stay null.
        _service = new OrderService(_context, new AlwaysOpenBusinessHours(), _stripe, new FixedSettingsService(), null!);
    }

    private static OrderCreateDto CollectionOrder(params OrderItemCreateDto[] items) => new()
    {
        CustomerName = "Test",
        CustomerPhone = "0000",
        OrderType = OrderType.Collection,
        Items = items.ToList()
    };

    [Fact]
    public async Task The_same_modifier_twice_on_one_line_is_refused()
    {
        var dto = CollectionOrder(new OrderItemCreateDto
        {
            MenuItemId = _pizzaId,
            Quantity = 1,
            ModifierOptionIds = new List<int> { _extraCheeseId, _extraCheeseId }
        });

        var result = await _service.CreateAsync(dto, applicationUserId: null);

        Assert.Null(result.Order);
        Assert.Contains("more than once", result.Error);
        Assert.Empty(_stripe.ChargedAmounts); // nobody was asked to pay for it
    }

    [Fact]
    public async Task An_order_below_stripes_minimum_is_refused_before_it_is_saved()
    {
        var dto = CollectionOrder(new OrderItemCreateDto { MenuItemId = _dipId, Quantity = 1 });

        var result = await _service.CreateAsync(dto, applicationUserId: null);

        Assert.Null(result.Order);
        Assert.Contains("£0.30", result.Error);
        Assert.Empty(_context.Orders);           // no orphaned Pending order left behind
        Assert.Empty(_stripe.ChargedAmounts);    // and no PaymentIntent created
    }

    [Fact]
    public async Task A_valid_order_is_charged_its_computed_total()
    {
        var dto = CollectionOrder(new OrderItemCreateDto
        {
            MenuItemId = _pizzaId,
            Quantity = 1,
            ModifierOptionIds = new List<int> { _extraCheeseId }
        });

        var result = await _service.CreateAsync(dto, applicationUserId: null);

        Assert.NotNull(result.Order);
        Assert.Equal(new[] { 9.00m }, _stripe.ChargedAmounts); // £8.00 pizza + £1.00 cheese

        // The stored snapshot is the same number Stripe was asked for, and it's what the response shows.
        Assert.Equal(9.00m, _context.Orders.Single().Total);
        Assert.Equal(9.00m, result.Order!.Total);
    }

    [Fact]
    public async Task A_driver_cannot_be_assigned_to_a_finished_order()
    {
        var driver = new Driver { Name = "Sam", Phone = "0000", ApplicationUser = new ApplicationUser { UserName = "sam" } };
        var order = new Order { CustomerName = "Test", OrderType = OrderType.Delivery, Status = OrderStatus.Completed };
        _context.AddRange(driver, order);
        _context.SaveChanges();

        var result = await _service.AssignDriverAsync(order.Id, new OrderDriverAssignmentDto { DriverId = driver.Id });

        Assert.Equal(DriverAssignmentResult.OrderNotActive, result);
        Assert.Null(order.DriverId);
    }
}
