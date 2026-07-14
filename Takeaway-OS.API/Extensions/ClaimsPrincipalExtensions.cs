using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Takeaway_OS.API.Extensions;

// An extension method adds a method to a type you don't own w/o subclassing it. 
// "this" -> makes it callable as User.GetApplicationUserId() instead of ClaimsPrincipalExtensions.Get...(User).
// Both the class and the method must be static.
//
// Lives here rather than on a base controller -> every controller that needs caller's identity should read it the same way 
// copying the claim lookup per controller is how one of them ends up subtly different.
public static class ClaimsPrincipalExtensions
{
    // AuthService signs the token with JwtRegisteredClaimNames.Sub ("sub"),
    // but JwtBearer's default MapInboundClaims=true RENAMES "sub" to ClaimTypes.NameIdentifier on theway in
    // so the claim that goes out is NOT the claim that comes back. Checking both means this keeps working whether
    // or not that mapping is ever turned off, instead of silently returning null (which would quietly
    // downgrade a logged-in customer's order to a guest order - a bug with no error message).
    //
    // Returns null for an anonymous caller, which is a legitimate state on the guest-checkout path.
    public static int? GetApplicationUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
