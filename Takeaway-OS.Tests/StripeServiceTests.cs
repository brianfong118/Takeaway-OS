using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Tests for StripeService.ConstructEvent (the webhook's signature gate)
// A forged "payment succeeded" call must NOT be accepted, or an attacker could mark any order Paid without paying
public class StripeServiceTests
{
    private const string WebhookSecret = "whsec_test_secret";

    private static StripeService BuildService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["STRIPE_WEBHOOK_SECRET"] = WebhookSecret
            })
            .Build();

        return new StripeService(config);
    }

    // Builds the Stripe-Signature header the way Stripe does: HMAC-SHA256 of "<timestamp>.<payload>"
    // keyed by the webhook secret, formatted as "t=<timestamp>,v1=<hex hash>".
    private static string SignPayload(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedContent = $"{timestamp}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        return $"t={timestamp},v1={signature}";
    }

    // A minimal but valid Stripe event JSON (enough for the SDK to parse after the signature passes)
    private const string PaymentSucceededJson =
        """{"id":"evt_test","object":"event","type":"payment_intent.succeeded","data":{"object":{"id":"pi_test","object":"payment_intent"}}}""";

    [Fact]
    public void ConstructEvent_rejects_a_forged_signature()
    {
        var service = BuildService();

        // Plausible but wrong signature (not computed with our secret).
        var result = service.ConstructEvent(PaymentSucceededJson, "t=123456789,v1=deadbeef");

        Assert.Null(result);
    }

    [Fact]
    public void ConstructEvent_rejects_an_empty_signature()
    {
        var service = BuildService();

        var result = service.ConstructEvent(PaymentSucceededJson, "");

        Assert.Null(result);
    }

    [Fact]
    public void ConstructEvent_rejects_a_payload_signed_with_the_wrong_secret()
    {
        var service = BuildService();

        // Correctly-formatted signature, but signed with a DIFFERENT secret than the service holds.
        var header = SignPayload(PaymentSucceededJson, "whsec_a_different_secret");
        var result = service.ConstructEvent(PaymentSucceededJson, header);

        Assert.Null(result);
    }

    [Fact]
    public void ConstructEvent_accepts_a_correctly_signed_payload()
    {
        var service = BuildService();

        var header = SignPayload(PaymentSucceededJson, WebhookSecret);
        var result = service.ConstructEvent(PaymentSucceededJson, header);

        Assert.NotNull(result);
        Assert.Equal("payment_intent.succeeded", result!.Type);
    }
}
