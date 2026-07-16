using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.Tests;

// Guards the rule that the PUBLIC menu hides disabled items.
// IClassFixture<TestWebAppFactory> = xUnit builds ONE factory (one booted app) and injects it into the constructor of each test class that declares it. 
// The factory is shared across all tests in the class, so the app host is booted only once per class, not once per test.
public class MenuItemsIntegrationTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public MenuItemsIntegrationTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_menu_returns_only_available_items()
    {
        // Arrange -> seed via a scope, using the real AppDbContext resolved from the app's own container.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();  // clean slate so re-running the suite doesn't stack duplicate rows
            db.Database.EnsureCreated();

            var category = new Category { Name = "Mains", DisplayOrder = 1 };
            // Setting the Category navigation lets EF fill in each item's CategoryId FK on save.
            db.MenuItems.Add(new MenuItem { Name = "Spring Roll",     Price = 3.50m, IsAvailable = true,  Category = category });
            db.MenuItems.Add(new MenuItem { Name = "Secret Off-Menu", Price = 9.99m, IsAvailable = false, Category = category });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(); // a real HttpClient wired to the in-memory server

        // Act
        var response = await client.GetAsync("/api/menuitems");

        // Assert
        response.EnsureSuccessStatusCode(); // throws (fails the test) on any non-2xx status code
        var items = await response.Content.ReadFromJsonAsync<List<MenuItemDto>>();

        Assert.NotNull(items);
        Assert.Single(items);                          // the disabled item must NOT appear
        Assert.Equal("Spring Roll", items![0].Name);   // and it's the available one
        Assert.Equal("Mains", items[0].CategoryName);  // non-empty CategoryName proves the Category JOIN actually ran
    }
}
