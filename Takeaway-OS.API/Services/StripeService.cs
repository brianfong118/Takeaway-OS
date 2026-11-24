using Stripe;

namespace Takeaway_OS.API.Services;

public class StripeService : IStripeService
{
    private readonly IConfiguration _configuration;
    private const string Currency = "gbp";

    public StripeService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amountInPounds, int orderId)
    {
        var options = new PaymentIntentCreateOptions
        {
            // Stripe amounts are in the smallest currency unit, as a whole number.
            // Round first so a stray fractional penny can't truncate wrong.
            Amount = (long)Math.Round(amountInPounds * 100m, MidpointRounding.AwayFromZero),
            Currency = Currency,
            // makes each payment traceable back to an order in the Stripe dashboard
            Metadata = new Dictionary<string, string> { ["orderId"] = orderId.ToString() }
        };

        // Uses the global StripeConfiguration.ApiKey set at startup in Program.cs.
        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(options);

        return new PaymentIntentResult
        {
            PaymentIntentId = intent.Id,
            ClientSecret = intent.ClientSecret
        };
    }

    public Event? ConstructEvent(string json, string signatureHeader)
    {
        var webhookSecret = _configuration["STRIPE_WEBHOOK_SECRET"]
            ?? throw new InvalidOperationException("STRIPE_WEBHOOK_SECRET is not configured.");

        try
        {
            // Recomputes the signature over the RAW body with our secret and checks it matches the header.
            // A forged or tampered payload fails here, so we never trust it.
            // throwOnApiVersionMismatch: false -> the signature is what matters; don't reject a genuine
            // webhook just because Stripe's account API version differs from the SDK's compiled version.
            return EventUtility.ConstructEvent(json, signatureHeader, webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            // Bad signature (or unparseable payload). null tells the controller to return 400 and stop.
            return null;
        }
    }

    public async Task RefundAsync(string paymentIntentId, int orderId)
    {
        // No Amount set -> Stripe refunds the FULL amount charged. Partial refunds (one wrong item)
        // would be a separate, deliberate feature with its own UI, not a side effect of cancelling.
        var options = new RefundCreateOptions { PaymentIntent = paymentIntentId };

        // An idempotency key makes a retried request return the FIRST request's result instead of
        // doing the work again. Stripe remembers each key for 24 hours.
        // It matters here because the refund and the status change are two separate systems: if
        // the refund succeeds but saving "Cancelled" to our database then fails, the Owner presses
        // cancel again, and without the key that second press would try to refund a second time.
        // One key per ORDER (not per click) is what makes every retry the same request.
        var requestOptions = new RequestOptions { IdempotencyKey = $"refund-order-{orderId}" };

        try
        {
            await new RefundService().CreateAsync(options, requestOptions);
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "charge_already_refunded")
        {
            // Already refunded, e.g. by hand in the Stripe dashboard, or a retry more than 24 hours
            // after the first attempt (when the idempotency key has expired). The money is back with
            // the customer either way, which is all this method promises, so it's not an error.
            // `when` is an exception filter: only this one error code is caught; every other
            // StripeException still propagates and stops the cancel.
        }
    }

    public async Task<bool> CancelPaymentIntentAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();

        try
        {
            await service.CancelAsync(paymentIntentId);
            return true;
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "payment_intent_unexpected_state")
        {
            // Stripe refuses to cancel a PaymentIntent that is not in a cancellable state. That
            // covers two very different situations, so ask Stripe which one this is:
            //   canceled            -> it was cancelled already (a retry): the goal is met.
            //   succeeded/processing -> the customer paid a moment ago. Report that, so the caller
            //                           leaves the order for the webhook to mark Paid.
            var intent = await service.GetAsync(paymentIntentId);
            return intent.Status == "canceled";
        }
    }
}
