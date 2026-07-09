using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// AddIdentity registers UserManager<ApplicationUser> (create users, hash/check passwords, assign roles), 
// RoleManager<IdentityRole<int>> (create/check roles) with the DI container

// AddEntityFrameworkStores tells Identity to persist all of that through AppDbContext,
// i.e. into the AspNetUsers/AspNetRoles/etc. tables that IdentityDbContext added.

// AddDefaultTokenProviders wires up the token generators Identity uses internally eg password-reset tokens 
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// The signing key that proves a JWT was issued by us and hasn't been tampered with.
// Read from config (which pulls from the JWT_SECRET env var / user-secrets) NEVER HARD CODED.
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT_SECRET is not configured.");

// AddIdentity defaults to cookie-based auth,
// doesn't suit a stateless API called from a separate frontend origin.
// AddAuthentication + AddJwtBearer overrides that: instead of a session cookie, 
// every request must carry an "Authorization: Bearer <token>" header
// this is the piece that actually reads and validates that header.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Reject tokens whose issuer/audience don't match ours, that have expired,
            // or whose signature doesn't verify against our signing key.
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Enables the [Authorize] / [Authorize(Roles = "Owner")] attributes on controllers.
builder.Services.AddAuthorization();

builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IMenuItemService, MenuItemService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IModifierGroupService, ModifierGroupService>();
builder.Services.AddScoped<IModifierOptionService, ModifierOptionService>();
builder.Services.AddScoped<IMenuItemModifierGroupService, MenuItemModifierGroupService>();
builder.Services.AddScoped<IOrderService, OrderService>();
// Register the "X"Service with the DI container, so that it can be injected into controllers or other services that require it.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Identity's AspNetRoles table starts empty. 
// This runs once at startup, before the app accepts any requests
// Inserts Owner/Driver/Customer if they don't already exist 
// so registration can assign a user to a role that's guaranteed to be there

// A "using" scope is needed because RoleManager is Scoped (tied to a request)
// but this code runs outside of any request, so a scope has to be created and disposed manually.
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    foreach (var role in Roles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(role));
        }
    }
}

// UseAuthentication decides WHO is calling (reads/validates the JWT).
// UseAuthorization decides WHAT they're allowed to do ([Authorize] checks).
// Order matters: authentication must run first so authorization has a validated user to check.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// builder.Services is the DI container itself, registering things into it before the app actually starts running.
// AddScoped<ICategoryService, CategoryService>(): maps interface ICategoryService to the concrete class CategoryService 
// Now when CategoriesController's constructor asks for an ICategoryService, .NET knows what to actually construct and hand it.

//Scoped refers to the lifetime of the object. There are three options in ASP.NET Core:

// Transient — a brand new instance every single time it's requested.
// Scoped — one instance per HTTP request. Same instance reused for the whole request if asked for multiple times, then thrown away.
// Singleton — one instance for the entire lifetime of the application, shared across all requests.

//Scoped is the right choice for CategoryService because it depends on AppDbContext
// and AppDbContext itself is registered as Scoped by default (due to AppDbContext)

//EF Core's DbContext is explicitly not thread-safe and isn't meant to be shared across concurrent requests
// so anything holding a reference to it needs to match that same "one per request" lifetime
//  If you registered CategoryService as a Singleton, it would hold onto its first AppDbContext forever and cause bugs/crashes on the second request.