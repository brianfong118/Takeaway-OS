using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Takeaway_OS.Tests;

// Makes every test request look like it arrived through a reverse proxy, the way production
// traffic arrives through Render's.
//
// Needed because the in-memory test server sets NO remote IP at all, and the forwarded-headers
// middleware deliberately trusts X-Forwarded-For when the remote IP is missing (for servers that
// can't report one). That made the bug invisible here: it only appears when the request comes from
// a real address that isn't loopback, which is exactly what Render's proxy is.
public class ProxiedTestWebAppFactory : TestWebAppFactory
{
    // A private-range address, like the one Render's proxy connects from. Deliberately NOT
    // loopback, since loopback is what the middleware's defaults already trust.
    private static readonly IPAddress ProxyAddress = IPAddress.Parse("10.0.0.5");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder); // keep the JWT config and in-memory database swap

        // An IStartupFilter wraps the app's own pipeline, so the middleware it adds runs BEFORE
        // everything in Program.cs, including UseForwardedHeaders. That is the position a real
        // proxy's address occupies: set by the connection, before any app code sees the request.
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, FakeProxyStartupFilter>());
    }

    private sealed class FakeProxyStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (HttpContext context, Func<Task> nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = ProxyAddress;
                await nextMiddleware();
            });
            next(app); // then the real pipeline from Program.cs
        };
    }
}

// Guards the rate limiter's per-client partitioning behind a reverse proxy.
//
// In production every request arrives from Render's proxy, so the only thing that tells two
// customers apart is the X-Forwarded-For header the proxy adds. If the forwarded-headers
// middleware ignores that header, every customer lands in ONE shared bucket and the 6th login
// attempt across the whole site gets a 429.
//
// Its own test class (and so its own app host, via IClassFixture) because the limiter's counters
// live in the app host for the length of the window: sharing a host with other tests would let
// their requests spend this test's budget.
public class RateLimitingIntegrationTests : IClassFixture<ProxiedTestWebAppFactory>
{
    private readonly ProxiedTestWebAppFactory _factory;

    public RateLimitingIntegrationTests(ProxiedTestWebAppFactory factory) => _factory = factory;

    // Sends one failed login attempt "from" the given client IP.
    private static async Task<HttpStatusCode> AttemptLoginAsync(HttpClient client, string clientIp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            // No such account, so an un-throttled attempt is a 401 and a throttled one is a 429.
            Content = JsonContent.Create(new { email = "nobody@example.test", password = "wrong-password" })
        };
        request.Headers.Add("X-Forwarded-For", clientIp);

        var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task Login_limit_is_counted_per_forwarded_client_ip()
    {
        var client = _factory.CreateClient();

        // Client A spends its whole budget (5 a minute, see Program.cs)...
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await AttemptLoginAsync(client, "203.0.113.10"));
        }

        // ...so its 6th attempt is refused,
        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptLoginAsync(client, "203.0.113.10"));

        // ...while client B, arriving through the same proxy, still has a full budget of its own.
        // Before the fix this was a 429 too: the header was ignored, so A and B were both just
        // "the proxy" and shared one bucket.
        Assert.Equal(HttpStatusCode.Unauthorized, await AttemptLoginAsync(client, "203.0.113.20"));
    }
}
