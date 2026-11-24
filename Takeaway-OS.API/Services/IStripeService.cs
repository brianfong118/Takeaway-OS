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
    // Stripe's smallest allowed charge in GBP. Any PaymentIntent below it is rejected by Stripe, so
    // OrderService refuses such an order before saving it. A constant on the interface (allowed
    // since C# 8) so callers can read Stripe's rule without depending on the concrete StripeService.
    const decimal MinimumCharge = 0.30m;

    // Creates a PaymentIntent for the given amount, in pounds. The service converts to pence
    // (Stripe works in the smallest currency unit) and returns the id + client secret.
    Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amountInPounds, int orderId);

    // Verifies a raw webhook payload against its Stripe-Signature header using STRIPE_WEBHOOK_SECRET.
    // Returns the parsed Event on success, or null if the signature is invalid/forged ,
    // (the caller turns null into a 400 and does nothing)
    // so a faked "payment succeeded" call can't mark an order Paid.
    Event? ConstructEvent(string json, string signatureHeader);

    // Refunds a PAID order's payment in full. Used when the Owner cancels an order the customer
    // has already been charged for, so cancelling and refunding are one action, not two.
    // orderId is used to make the call safe to retry (see the idempotency key in StripeService).
    // Throws on any Stripe failure, so the caller never marks the order Cancelled while the
    // customer's money is still taken.
    Task RefundAsync(string paymentIntentId, int orderId);

    // Cancels an UNPAID PaymentIntent so it can never be paid afterwards.
    // Returns true when the PaymentIntent is now cancelled (including "it already was").
    // Returns false when it can't be cancelled because the money has already been taken or is
    // mid-flight (status succeeded / processing): the customer paid in the moment before the
    // cancel, and the caller must not treat the order as unpaid.
    Task<bool> CancelPaymentIntentAsync(string paymentIntentId);
}
