using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous] // turn off [Authorize] for this endpoint -> no JWT required to register
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // UseAuthentication() (Program.cs) already decoded any Authorization header into User
        // true only if a valid Owner JWT was sent
        var callerIsOwner = User.IsInRole(Roles.Owner);
        var result = await _authService.RegisterAsync(request, callerIsOwner);

        return result.Outcome switch
        {
            RegisterOutcome.Success => Ok(result.Response),
            RegisterOutcome.InvalidRole => BadRequest(result.Error),             // Role wasn't Owner/Driver/Customer
            RegisterOutcome.OwnerRegistrationClosed => StatusCode(403, result.Error), // an Owner already exists
            RegisterOutcome.DriverRequiresOwnerAuth => StatusCode(403, result.Error), // callerIsOwner was false
            RegisterOutcome.IdentityError => BadRequest(result.Error),          // duplicate email, weak password, etc
            _ => BadRequest(result.Error)
        };
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result.Outcome == LoginOutcome.InvalidCredentials) return Unauthorized();
        return Ok(result.Response);
    }
}
