namespace Takeaway_OS.API.DTOs;

// Read-only. No create/update counterpart 
// Rows genereated by OrderService from submitted ModifierOptionIds, 
//  never constructed directly by the client.

public class OrderItemModifierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
}