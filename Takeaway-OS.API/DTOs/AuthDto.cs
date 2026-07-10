namespace Takeaway_OS.API.DTOs;

// Role is a string here (not the Roles enum-like consts) cus it comes off the wire as JSON
// AuthService validates it against Roles.All before doing anything with it.

// Name/Phone are required for Customer/Driver (used to create their linked profile row) 
// meaningless for Owner, AuthService only reads them when Role isn't Owner.
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
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
