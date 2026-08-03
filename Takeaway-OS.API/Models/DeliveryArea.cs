namespace Takeaway_OS.API.Models;

// One postcode district the shop is willing to deliver to. The allowed area is a
// variable-length list of Owner data, so it gets its own table rather than a column on
// the RestaurantSettings singleton - same call as OpeningHours, and for the same reason:
// a unique index makes "no duplicate districts" a database guarantee instead of a
// string-splitting check in C#.
//
// Deliberately NOT seeded by the migration. An empty table means the shop delivers
// NOWHERE until the Owner adds a district (see IDeliveryAreaService for why fail-closed
// won over fail-open). Contrast RestaurantSettings, which IS seeded, because a singleton
// row that doesn't exist is a missing-migration bug rather than an unconfigured setting.
public class DeliveryArea
{
    public int Id { get; set; }

    // The OUTWARD code only not a full postcode
    // Stored normalised: uppercase, whitespace stripped. Normalising on the way IN is what
    // lets the match be plain string equality on the way out 
    public string OutwardCode { get; set; } = string.Empty;
}
