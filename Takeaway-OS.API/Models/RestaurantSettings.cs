namespace Takeaway_OS.API.Models;

// Deliberately a single-row table (Id always == 1)
// Standard way to store app-wide config that the Owner can change at runtime:
// an env var can't be edited without a redeploy
// Key/value table -> string-typed lookups + parsing for just two typed fields.
public class RestaurantSettings
{
    // Fixed at 1 by the seed in AppDbContext. Nothing ever creates a second row.
    public const int SingletonId = 1;

    public int Id { get; set; }

    // The manual holiday/closure override.
    // Owner can shut for Christmas Day w/o deleting and re-adding that weekday's rows.
    
    // NOTE: this is a plain flag with no expiry, so it stays on until the Owner turns it off.
    // Forgetting to flip it back means the shop silently takes no orders 
    // Chosen over an auto-expiring ClosedUntil for simplicity
    public bool IsTemporarilyClosed { get; set; } = false;

    // Shown to customers on the "we're closed" banner
    // Empty when open, or when the Owner closes without giving a reason.
    public string ClosureReason { get; set; } = string.Empty;

    // The CURRENT delivery fee. Not what any existing order was charged:
    // every order snapshots this onto Order.DeliveryFee at creation, so changing
    // it here only affects orders placed from now on
    public decimal DeliveryFee { get; set; } = 0m;
}
