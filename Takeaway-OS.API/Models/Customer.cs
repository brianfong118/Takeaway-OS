namespace Takeaway_OS.API.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    // 1:1 link to the Identity login this profile belongs to.
    public int ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; } = null!;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();
}
