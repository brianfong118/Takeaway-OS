using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly string _jwtSecret;

    public AuthService(UserManager<ApplicationUser> userManager, AppDbContext context, IConfiguration configuration)
    {
        _userManager = userManager;
        _context = context;
        _configuration = configuration;

        // Same fail-fast check as Program.cs, if this is missing, every register/login call
        // would otherwise throw deep inside token generation instead of at a clear point.
        _jwtSecret = configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException("JWT_SECRET is not configured.");
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, bool callerIsOwner)
    {
        if (!Roles.All.Contains(request.Role))
        {
            return new RegisterResult
            {
                Outcome = RegisterOutcome.InvalidRole,
                Error = $"Role must be one of: {string.Join(", ", Roles.All)}."
            };
        }

        // Self-closing bootstrap: role: Owner is only accepted while zero Owners exist.
        // Once the first one is created, this branch blocks every future attempt 
        // there's deliberately no separate "admin-only" path to add more Owners later.
        if (request.Role == Roles.Owner)
        {
            var existingOwners = await _userManager.GetUsersInRoleAsync(Roles.Owner);
            if (existingOwners.Count > 0)
            {
                return new RegisterResult
                {
                    Outcome = RegisterOutcome.OwnerRegistrationClosed,
                    Error = "Owner registration is closed - an Owner account already exists."
                };
            }
        }

        // Driver accounts are created by an Owner on the Driver's behalf, not self-registered.
        if (request.Role == Roles.Driver && !callerIsOwner)
        {
            return new RegisterResult
            {
                Outcome = RegisterOutcome.DriverRequiresOwnerAuth,
                Error = "Only an authenticated Owner can create a Driver account."
            };
        }

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new RegisterResult
            {
                Outcome = RegisterOutcome.IdentityError,
                Error = string.Join(" ", createResult.Errors.Select(e => e.Description))
            };
        }

        await _userManager.AddToRoleAsync(user, request.Role);

        // Owner has no separate profile table  
        // ApplicationUser row plus the Owner role claim is the whole account
        // Customer/Driver each get a linked profile row for the extra fields Identity doesn't hold.
        if (request.Role == Roles.Customer)
        {
            _context.Customers.Add(new Customer
            {
                Name = request.Name,
                Phone = request.Phone,
                ApplicationUserId = user.Id
            });
            await _context.SaveChangesAsync();
        }
        else if (request.Role == Roles.Driver)
        {
            _context.Drivers.Add(new Driver
            {
                Name = request.Name,
                Phone = request.Phone,
                ApplicationUserId = user.Id,
                IsAvailable = true
            });
            await _context.SaveChangesAsync();
        }

        var token = GenerateJwt(user, request.Role);
        return new RegisterResult
        {
            Outcome = RegisterOutcome.Success,
            Response = new AuthResponse { Token = token, Email = user.Email!, Role = request.Role }
        };
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return new LoginResult { Outcome = LoginOutcome.InvalidCredentials };
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return new LoginResult { Outcome = LoginOutcome.InvalidCredentials };
        }

        // Every account has exactly one role 
        // Set once at registration and never changed, therfore taking the first (only) role here is safe.
        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        var token = GenerateJwt(user, role);

        return new LoginResult
        {
            Outcome = LoginOutcome.Success,
            Response = new AuthResponse { Token = token, Email = user.Email!, Role = role }
        };
    }

    private string GenerateJwt(ApplicationUser user, string role)
    {
        // sub + role are all [Authorize(Roles = "...")] and any "who is this" lookup need.
        // ClaimTypes.Role (not a raw "role" string) is what ASP.NET's role-checking reads.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Role, role) 
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)); 
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256); 

        // No refresh tokens in v1 
        // 1-day expiry means a customer/driver/owner has to log in daily [WILL REVISIT!!!]
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token); // returns the compact JWT string that the client will send in the Authorization header
    }
}
