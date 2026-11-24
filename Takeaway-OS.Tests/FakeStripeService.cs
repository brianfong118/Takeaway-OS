using Stripe;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// A stand-in for StripeService, so OrderService can be tested without network calls or a Stripe key.
// It records what it was asked to do, and a couple of switches make it misbehave the way the real
// Stripe can (a payment that completed a moment ago, a refund that fails).
//
// This is why OrderService depends on the IStripeService interface rather than the concrete class:
// the test hands it this fake through the same constructor parameter the real one uses.
internal sealed class FakeStripeService : IStripeService
{
    public List<string> RefundedPaymentIntents { get; } = new();
    public List<string> CancelledPaymentIntents { get; } = new();

    // The amount of every PaymentIntent created, so tests can check what the customer was charged.
    public List<decimal> ChargedAmounts { get; } = new();

    // Simulates "the customer paid in the moment before we tried to cancel": cancel reports false.
    public bool PaymentAlreadyTaken { get; set; }

    // Simulates Stripe being down or rejecting the refund: RefundAsync throws.
    public bool FailRefunds { get; set; }

    // Simulates Stripe being unreachable when cancelling: CancelPaymentIntentAsync throws.
    public bool FailCancels { get; set; }

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amountInPounds, int orderId)
    {
        ChargedAmounts.Add(amountInPounds);
        return Task.FromResult(new PaymentIntentResult
        {
            PaymentIntentId = $"pi_fake_{orderId}",
            ClientSecret = $"pi_fake_{orderId}_secret_fake"
        });
    }

    // Signature checking has its own tests against the real StripeService (StripeServiceTests).
    public Event? ConstructEvent(string json, string signatureHeader) => throw new NotSupportedException();

    public Task RefundAsync(string paymentIntentId, int orderId)
    {
        if (FailRefunds) throw new StripeException("Simulated Stripe failure");

        RefundedPaymentIntents.Add(paymentIntentId);
        return Task.CompletedTask;
    }

    public Task<bool> CancelPaymentIntentAsync(string paymentIntentId)
    {
        if (FailCancels) throw new StripeException("Simulated Stripe failure");
        if (PaymentAlreadyTaken) return Task.FromResult(false);

        CancelledPaymentIntents.Add(paymentIntentId);
        return Task.FromResult(true);
    }
}
