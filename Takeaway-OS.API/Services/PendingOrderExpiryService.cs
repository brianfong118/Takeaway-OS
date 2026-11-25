namespace Takeaway_OS.API.Services;

// Cancels orders that were placed but never paid for.
//
// Why it's needed: an order is created (Pending) BEFORE the customer pays, and nothing ever ends it
// if they walk away. Its PaymentIntent stays payable forever, so a customer who leaves the payment
// tab open at 21:58 can pay at 23:30 and land a Paid order on the board after the shop has closed.
// Opening hours are only checked when the order is created, so nothing else stops that.
//
// A BackgroundService is ASP.NET Core's built-in way to run work that isn't triggered by an HTTP
// request. The host starts ExecuteAsync once when the app starts and cancels its token when the app
// shuts down. It lives as long as the app (registered with AddHostedService in Program.cs).
public class PendingOrderExpiryService : BackgroundService
{
    // How long a customer has to pay. 30 minutes is far longer than any real checkout (card entry
    // plus a 3D Secure check takes a couple of minutes), yet short enough that an order paid at the
    // last moment is still one the kitchen expects.
    internal static readonly TimeSpan UnpaidTimeout = TimeSpan.FromMinutes(30);

    // How often to look. An order can therefore live up to UnpaidTimeout + CheckInterval, which is fine.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    // A payment Stripe recorded later than this after the order was placed is one the running job
    // would already have voided, so it can only have got through because the server was asleep.
    // OrderService.MarkOrderPaidAsync refunds such a payment instead of accepting it. The extra
    // CheckInterval is the slack for a payment the job simply hadn't reached yet, which is accepted.
    internal static readonly TimeSpan LatePaymentThreshold = UnpaidTimeout + CheckInterval;

    // How long after startup the first pass runs. Not zero, so the integration tests (which boot the
    // app for a few seconds and reset the in-memory database while doing so) finish before a pass
    // could touch it; short, so a server that wakes from sleep clears its backlog within a minute
    // instead of waiting a full CheckInterval.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingOrderExpiryService> _logger;

    // IServiceScopeFactory, NOT IOrderService: this service is a singleton (one instance for the whole
    // app's life), while OrderService and its AppDbContext are scoped (one per request). Injecting a
    // scoped service into a singleton would keep one DbContext alive forever, which is exactly the
    // lifetime bug described at the bottom of Program.cs. Instead, each pass creates its own scope.
    public PendingOrderExpiryService(IServiceScopeFactory scopeFactory, ILogger<PendingOrderExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A first pass shortly after startup, not one CheckInterval later. On a host that sleeps when
        // idle, every wake-up is a startup, and waiting a full interval would leave a whole night's
        // abandoned orders payable for 5 more minutes. Task.Delay with the stopping token throws
        // OperationCanceledException if the app shuts down during the wait, which simply ends this
        // method, the normal way a BackgroundService stops.
        await Task.Delay(StartupDelay, stoppingToken);
        await RunPassAsync();

        // PeriodicTimer ticks every CheckInterval. WaitForNextTickAsync waits for the next tick and
        // returns true, or throws OperationCanceledException once the app starts shutting down, which
        // ends the loop and the method. Unlike System.Timers.Timer it never runs two passes at once:
        // a slow pass just delays the next tick instead of overlapping it.
        using var timer = new PeriodicTimer(CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunPassAsync();
        }
    }

    // One pass, with every failure caught.
    private async Task RunPassAsync()
    {
        try
        {
            await ExpireOnceAsync();
        }
        catch (Exception ex)
        {
            // Catch EVERYTHING here. An exception escaping ExecuteAsync stops the whole app by
            // default (.NET 8+), and a database blip at 3am must not take ordering down with it.
            // The next tick simply tries again.
            _logger.LogError(ex, "Expiring unpaid orders failed; retrying on the next pass.");
        }
    }

    private async Task ExpireOnceAsync()
    {
        // `await using` disposes the scope (and the DbContext inside it) when this method ends,
        // the same thing ASP.NET does automatically at the end of every HTTP request.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.ExpireStalePendingOrdersAsync(DateTime.UtcNow - UnpaidTimeout);

        if (result.Expired > 0)
            _logger.LogInformation("Cancelled {Count} order(s) left unpaid for over {Minutes} minutes.",
                result.Expired, UnpaidTimeout.TotalMinutes);

        // Warnings, because each is an order whose payment state a human may want to check.
        foreach (var orderId in result.FailedOrderIds)
            _logger.LogWarning("Could not void the payment for unpaid order {OrderId}; it stays Pending and will be retried.", orderId);
    }
}
