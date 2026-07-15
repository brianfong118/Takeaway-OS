using System.ComponentModel.DataAnnotations;

namespace Takeaway_OS.API.DTOs;

// What GET /api/customers/me returns: the caller's own profile.
public class CustomerProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty; // comes from the linked ApplicationUser(Identity), NOT Customer row 
}

// Body for PUT /api/customers/me. Only Name/Phone are editable.
// NO EMAIL: it's the Identity username the customer logs in with, so changing it is
// an auth concern (re-verification, uniqueness) rather than a profile edit 
public class CustomerProfileUpdateDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;
}
