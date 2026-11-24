using System.ComponentModel.DataAnnotations;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.DTOs;

// Role is a string here (not the Roles enum-like consts) cus it comes off the wire as JSON
// AuthService validates it against Roles.All before doing anything with it.

// Name/Phone are required for Customer/Driver (used to create their linked profile row)
// meaningless for Owner, AuthService only reads them when Role isn't Owner.
public class RegisterRequest : IValidatableObject
{
    // [EmailAddress] is deliberately loose (it checks for a single '@' with text either side), but
    // Identity checks NOTHING about the format unless RequireUniqueEmail is on, so without this
    // "bob" would be accepted as an email. The login is the email, so a typo locks the person out.
    // 256 is Identity's own column length for Email/UserName.
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    // Only presence here. Length and strength rules belong to Identity's password options in
    // Program.cs, so they are defined once and reported in Identity's own words.
    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    [MaxLength(100)] // same limits as the Customer/Driver profile DTOs and the register form
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    // Conditional, so it can't be an attribute: a Customer or Driver gets a profile row built from
    // Name and Phone, and a blank one would leave a nameless driver on the owner's dropdown.
    // The Owner has no profile row, so the bootstrap request doesn't have to invent them.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Role == Roles.Owner) yield break; // `yield break` ends an iterator with no (more) results

        if (string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult("Name is required.", new[] { nameof(Name) });

        if (string.IsNullOrWhiteSpace(Phone))
            yield return new ValidationResult("Phone is required.", new[] { nameof(Phone) });
    }
}

public class LoginRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

// What the client gets back after a successful register/login 
// JWT plus just enough user info to render "logged in as X" w/o a follow-up request.
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
