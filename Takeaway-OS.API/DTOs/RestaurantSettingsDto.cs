using System.ComponentModel.DataAnnotations;

namespace Takeaway_OS.API.DTOs;

// shape returned by GET /api/settings (anonymous)
// The checkout has to show the fee BEFORE the order exists, so this read is public.
// Only fields the customer is allowed to see go here: RestaurantSettings also carries the
// closure flag, which is already served, worded for customers, by /api/openinghours/status.
public class RestaurantSettingsDto
{
    public decimal DeliveryFee { get; set; }
}

// shape accepted by PUT /api/settings (Owner)
// Separate type from the read for the same reason as everywhere else: one endpoint's shape
// shouldn't drift because the other's did.
public class RestaurantSettingsUpdateDto
{
    // Range, not a service-layer check: [ApiController] turns a violation into a 400 before
    // the service runs. A negative fee would subtract from the total and send Stripe a smaller
    // amount than the food costs; the upper bound only exists to catch a mistyped 250 for 2.50.
    [Range(0, 100, ErrorMessage = "DeliveryFee must be between 0 and 100.")]
    public decimal DeliveryFee { get; set; }
}
