using Takeaway_OS.API.DTOs;

namespace Takeaway_OS.API.Services;

// Register can fail in several different ways that map to different HTTP responses,
// Unlike OrderCreateResult's single Error, this needs an enum the controller can switch on.
public enum RegisterOutcome
{
    Success,
    InvalidRole,              // Role wasn't Owner/Driver/Customer -> 400
    OwnerRegistrationClosed,  // an Owner already exists, self-closing bootstrap -> 403
    DriverRequiresOwnerAuth,  // Role: Driver but caller isn't an authenticated Owner -> 403
    IdentityError             // duplicate email, weak password, etc (from Identity itself) -> 400
}

public class RegisterResult
{
    public RegisterOutcome Outcome { get; set; }
    public AuthResponse? Response { get; set; } // set only when Outcome == Success
    public string? Error { get; set; } // set whenever Outcome != Success
}

public enum LoginOutcome
{
    Success,
    InvalidCredentials
}

public class LoginResult
{
    public LoginOutcome Outcome { get; set; }
    public AuthResponse? Response { get; set; } // set only when Outcome == Success
}

public interface IAuthService
{
    // callerIsOwner is decided by the controller (from the caller's JWT, if any) then passed in 
    // the service shouldn't reach into HttpContext itself, that's an HTTP concern
    Task<RegisterResult> RegisterAsync(RegisterRequest request, bool callerIsOwner);
    Task<LoginResult> LoginAsync(LoginRequest request);
}
