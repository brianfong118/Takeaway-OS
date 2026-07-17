using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly IOrderService _orderService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IStripeService stripeService, IOrderService orderService, ILogger<WebhooksController> logger)
    {
        _stripeService = stripeService;
        _orderService = orderService;
        _logger = logger;
    }

    // Stripe calls this server-to-server when a payment's state changes. 
    // NOT the browser reporting success -> signature (NOT a JWT) is what authenticates it.
    [HttpPost("stripe")]
    [AllowAnonymous] // Stripe isn't a logged-in user; the Stripe-Signature check below is the real auth
    public async Task<IActionResult> Stripe()
    {
        // Signature verification needs the EXACT raw bytes Stripe sent 
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();

        // .ToString() gives "" (not null) when the header is absent, so we can reject a missing signature outright
        // a genuine Stripe call always carries one, and passing null into the
        // verifier would throw a different exception than the signature-mismatch we handle below.
        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Rejected a Stripe webhook with no signature header.");
            return BadRequest();
        }

        var stripeEvent = _stripeService.ConstructEvent(json, signatureHeader);
        if (stripeEvent is null)
        {
            // Invalid/forged signature. Reject and do nothing.
            _logger.LogWarning("Rejected a Stripe webhook with an invalid signature.");
            return BadRequest();
        }

        // We only act on a confirmed successful payment. Every other event type is acknowledged
        // with 200 (so Stripe stops resending) but otherwise ignored.
        if (stripeEvent.Type == EventTypes.PaymentIntentSucceeded)
        {
            var paymentIntent = (PaymentIntent)stripeEvent.Data.Object;
            var result = await _orderService.MarkOrderPaidAsync(paymentIntent.Id);

            // All outcomes return 200 -> the event was received and handled 
            // but log the odd ones, since they signal a data mismatch worth a human looking at.
            switch (result)
            {
                case OrderPaymentResult.OrderNotFound:
                    _logger.LogWarning("Stripe payment {PaymentIntentId} succeeded but no order carries that id.", paymentIntent.Id);
                    break;
                case OrderPaymentResult.NotPayable:
                    _logger.LogWarning("Stripe payment {PaymentIntentId} succeeded for an order that isn't Pending (may need a manual refund).", paymentIntent.Id);
                    break;
            }
        }

        return Ok();
    }
}
