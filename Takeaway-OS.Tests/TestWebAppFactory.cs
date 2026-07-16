using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Takeaway_OS.API.Data;

namespace Takeaway_OS.Tests;

// Boots the real app in memory for integration tests, with two changes from production:
//   1. config values the app reads at startup are supplied here (so it doesn't throw for a missing JWT_SECRET);
//   2. the Postgres database is swapped for an in-memory one, so tests need no real database and stay isolated
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs reads JWT_SECRET and throws if it's absent, so provide one (plus the issuer/audience)
        // These are test-only values; nothing real is signed with them.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "integration-test-signing-key-at-least-32-chars",
                ["Jwt:Issuer"] = "TakeawayOS",
                ["Jwt:Audience"] = "TakeawayOS"
            });
        });

        // Runs AFTER Program.cs has registered its services, so we can find the real (Npgsql) DbContext
        // registration, remove it, and register an in-memory one in its place. The rest of the app is untouched.
        builder.ConfigureServices(services =>
        {
            var dbOptions = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbOptions is not null) services.Remove(dbOptions);

            // Program.cs already registered Npgsql's provider services in the app container. 
            // If we just add the in-memory provider too, EF sees TWO providers and throws 
            // "Only a single database provider can be registered". 
            // Rather than hunt down every Npgsql service, build a SEPARATE service provider that
            // contains only the in-memory provider, and point this DbContext at it via UseInternalServiceProvider.
            // The context now resolves its EF services from this isolated provider, never from the app container's.
            var inMemoryEfServices = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            // One fixed database name = one shared in-memory store for the app host. Tests that need a clean
            // slate call EnsureDeleted/EnsureCreated themselves before seeding (see the integration test).
            services.AddDbContext<AppDbContext>(options => options
                .UseInMemoryDatabase("IntegrationTestDb")
                .UseInternalServiceProvider(inMemoryEfServices));
        });
    }
}
