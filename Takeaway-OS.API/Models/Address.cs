namespace Takeaway_OS.API.Models;

public class Address
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty; // e.g. "Home", "Work"
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
}
