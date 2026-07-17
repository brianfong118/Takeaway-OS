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
}
