namespace Takeaway_OS.API.Models;

public class OrderItemModifier
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }

    // Both snapshotted from ModifierOption at order-creation time.
    // Deliberately NOT a ModifierOptionId FK — same principle as OrderItem
    // having no live MenuItemId. If the option is later renamed, re-priced,
    // or deleted entirely, this row is untouched.
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }

    public OrderItem OrderItem { get; set; } = null!;
}