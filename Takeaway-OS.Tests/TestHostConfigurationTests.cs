using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Takeaway_OS.Tests;

// Guards the test host itself: integration tests must run on the values TestWebAppFactory supplies,
// never on whatever secrets the machine running them happens to have.
//
// This went wrong once without anyone noticing. The factory's values arrived too late for
// Program.cs, so locally the developer's real user-secrets filled the gap and every test passed,
// while CI, with no user-secrets, failed with "JWT_SECRET is not configured".
public class TestHostConfigurationTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public TestHostConfigurationTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public void The_test_host_uses_the_factorys_values_not_the_developers_secrets()
    {
        // Resolving Services boots the app, running Program.cs exactly as the other tests do.
        var config = _factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("integration-test-signing-key-at-least-32-chars", config["JWT_SECRET"]);

        // Program.cs copies STRIPE_SECRET_KEY into Stripe's global setting at startup. Blank here
        // means even a machine with a real Stripe key in user-secrets can't make a test call Stripe.
        Assert.True(string.IsNullOrEmpty(Stripe.StripeConfiguration.ApiKey));
    }
}
