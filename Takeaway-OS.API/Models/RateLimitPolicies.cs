namespace Takeaway_OS.API.Models;

// Names of the rate-limiting policies registered in Program.cs, kept here for the same reason as
// Roles: the name is written in two places that MUST agree : AddPolicy() in Program.cs and
// [EnableRateLimiting] on the controller action , a typo in either silently means the action
// gets no limit at all rather than failing loudly.
//
// Sits in Models alongside Roles, which is the existing home for shared constant strings, even
// though neither is really an entity.
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Register = "register";
    public const string CreateOrder = "create-order";
}
