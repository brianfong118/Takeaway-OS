namespace Takeaway_OS.API.Models;

// Central list of valid role names, so "Owner"/"Driver"/"Customer" strings
// aren't typed by hand (and possibly mistyped) across the codebase.
public static class Roles
{
    public const string Owner = "Owner";
    public const string Driver = "Driver";
    public const string Customer = "Customer";

    public static readonly string[] All = [Owner, Driver, Customer];
}
