using System.ComponentModel.DataAnnotations;

namespace Takeaway_OS.API.DTOs;

public class AddressDto  // shape returned by GET /api/addresses
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    // No CustomerId - the caller only ever gets their OWN addresses
    // echoing the owning customer back would be noise at best, and a hint to start guessing IDs at worst.
}

public class AddressCreateDto
{
    [MaxLength(50)]
    public string Label { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Line1 { get; set; } = string.Empty;

    [MaxLength(100)] 
    public string Line2 { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Postcode { get; set; } = string.Empty;

    // Requesting the default slot is allowed; CLAIMING it is not. Server decides
    // See AddressService, which force-defaults a customer's first address regardless of what's sent.
    public bool IsDefault { get; set; }

    // No CustomerId, deliberately - same rule as OrderCreateDto. The owning customer comes from
    // the validated JWT, never the request body, or anyone could write into someone else's address book.
}

public class AddressUpdateDto
{
    [MaxLength(50)]
    public string Label { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Line1 { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Line2 { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Postcode { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}
