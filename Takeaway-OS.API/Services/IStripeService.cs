using Stripe;

namespace Takeaway_OS.API.Services;

// What CreatePaymentIntentAsync hands back: the id we persist on the Order (the webhook's lookup key) 
// and the client secret the browser needs to complete the payment.
public class PaymentIntentResult
{
    public string PaymentIntentId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

// The boundary between our app and the Stripe SDK. Only this service calls Stripe directly,
// so controllers/other services never touch the Stripe API surface (mirrors the services-layer rule).
public interface IStripeService
{
    // Creates a PaymentIntent for the given amount, in pounds. The service converts to pence
    // (Stripe works in the smallest currency unit) and returns the id + client secret.
    Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amountInPounds, int orderId);

    // Verifies a raw webhook payload against its Stripe-Signature header using STRIPE_WEBHOOK_SECRET.
    // Returns the parsed Event on success, or null if the signature is invalid/forged ,
    // (the caller turns null into a 400 and does nothing)
    // so a faked "payment succeeded" call can't mark an order Paid.
    Event? ConstructEvent(string json, string signatureHeader);
}
