using Microsoft.AspNetCore.Identity;

namespace Takeaway_OS.API.Models;

// IdentityUser<int> already provides Id (int), UserName, Email, PasswordHash, PhoneNumber, etc.
// Empty cuz profile data (name, phone, availability) lives on Customer/Driver instead
//Exists to satisfy Identity's generic type requirements and to allow EF Core to create the AspNetUsers table with int PK.
// <int> keeps the Identity primary key type consistent with every other entity's int Id in this app
public class ApplicationUser : IdentityUser<int>
{
}
