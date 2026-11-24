using System.ComponentModel.DataAnnotations;

namespace Takeaway_OS.API.DTOs;

public class DriverDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}

// No DriverCreateDto - Drivers are created via POST /api/auth/register (role: "Driver")
// see RegisterRequest in AuthDto.cs.

public class DriverUpdateDto
{
    // Same limits as the Customer profile: the name goes on the owner's driver dropdown, and the
    // phone number is how the kitchen reaches a driver mid-delivery, so neither can be blank.
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    public bool IsAvailable { get; set; }
}