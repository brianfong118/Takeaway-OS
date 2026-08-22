using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CORS ---
// The trusted frontend origins, read from config so they differ per environment:
// http://localhost:5173 (Vite) in Development, the real Vercel URL in Production (set via env var).
// An explicit allow-list, never AllowAnyOrigin(), because this API carries JWTs.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

const string FrontendCorsPolicy = "FrontendPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins) // exact origins only
              .AllowAnyHeader()  // allow the browser to send Content-Type, Authorization, etc.
              .AllowAnyMethod()); // GET/POST/PUT/DELETE + the OPTIONS preflight
    // No AllowCredentials(): the JWT rides in an Authorization header, not a cookie,
    // so we don't opt into credentialed (cookie) requests.
});

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

// Stripe.NET reads this one global key for every API call (e.g. creating a PaymentIntent).
// Set once here from config/env; kept server-side only. 
// If missing, Stripe calls fail w/ "No API key provided" (only affects order creation, not the rest of the app)
Stripe.StripeConfiguration.ApiKey = builder.Configuration["STRIPE_SECRET_KEY"];

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

// --- Rate limiting ---
// Covers the three endpoints that are anonymous, public, and WRITE something. Each is limited for
// a different reason:
//
//   login       - asks for credentials, so it is the one endpoint worth guessing at. Unlimited,
//                 an attacker gets endless attempts at a known Owner email address.
//   register    - creates a row and an Identity user per call.
//   create-order- creates an order AND a Stripe PaymentIntent per call, so a loop against it costs
//                 database rows on a free tier and hits Stripe's own limits on our account.
//
// Applied per-endpoint with [EnableRateLimiting] rather than globally: the rest of the API is
// either behind a JWT or is the menu, and a customer scrolling a menu should never meet a 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // All three policies are the same shape and differ only in budget, so the shape is written once.
    // Partitioned by client IP, so one person hammering an endpoint cannot lock everyone else out.
    // RemoteIpAddress is null only when there is no remote IP at all (in-memory test requests), and
    // those all then share the single "unknown" bucket - fine while no test posts in a loop.
    static RateLimitPartition<string> PerIp(HttpContext httpContext, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    options.AddPolicy(RateLimitPolicies.Login, ctx => PerIp(ctx, 5));       // mistyping a password a few times
    options.AddPolicy(RateLimitPolicies.Register, ctx => PerIp(ctx, 3));    // signing up is a once-ever action
    options.AddPolicy(RateLimitPolicies.CreateOrder, ctx => PerIp(ctx, 10)); // demo visitors do place several
});

builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IMenuItemService, MenuItemService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IModifierGroupService, ModifierGroupService>();
builder.Services.AddScoped<IModifierOptionService, ModifierOptionService>();
builder.Services.AddScoped<IMenuItemModifierGroupService, MenuItemModifierGroupService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBusinessHoursService, BusinessHoursService>();
builder.Services.AddScoped<IRestaurantSettingsService, RestaurantSettingsService>();
builder.Services.AddScoped<IDeliveryAreaService, DeliveryAreaService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IStripeService, StripeService>();
// Register the "X"Service with the DI container, so that it can be injected into controllers or other services that require it.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- Startup database work ---
// Both steps below run once at startup, before the app accepts any requests, and both
// need a service scope: AppDbContext and RoleManager are registered Scoped (tied to an
// HTTP request), but this code runs outside any request, so a scope has to be created
// and disposed manually. One scope covers both, and the ORDER inside it matters —
// migrations must create the AspNetRoles table before role seeding tries to write to it.
using (var scope = app.Services.CreateScope())
{
    // 1. Apply any pending EF Core migrations
    // Guarded by IsRelational() because migrations are a relational-provider concept: the
    // integration tests swap Postgres for the in-memory provider, which has no schema to
    // migrate and throws on MigrateAsync. Those tests build their schema with EnsureCreated.
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }

    // 2. Seed the roles.
    // Identity's AspNetRoles table starts empty.
    // Inserts Owner/Driver/Customer if they don't already exist
    // so registration can assign a user to a role that's guaranteed to be there.
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    foreach (var role in Roles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(role));
        }
    }
}

// Must run FIRST, before anything reads the client's IP. 
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor, 
    ForwardLimit = 1,
    KnownIPNetworks = { },
    KnownProxies = { }
});

// UseCors must come before auth: a preflight OPTIONS request carries no JWT, so CORS
// has to handle it before UseAuthentication/UseAuthorization would otherwise reject it.
app.UseCors(FrontendCorsPolicy);

// After UseCors so a preflight OPTIONS is answered by CORS and never spends a request from
// someone's five-a-minute budget; before auth because a limited endpoint should be turned away
// on cost grounds without the server doing password-hashing work for an attacker first.
app.UseRateLimiter();

// UseAuthentication decides WHO is calling (reads/validates the JWT).
// UseAuthorization decides WHAT they're allowed to do ([Authorize] checks).
// Order matters: authentication must run first so authorization has a validated user to check.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// The only route outside /api, and the only one not on a controller 
//
// It exists for two reasons that turned out to be the same reason:
//   1. The host's health check needs a path that returns 200 when the app is alive. Pointed at a
//      path that does not exist, a health check fails forever, and the platform responds by
//      restarting a container that was never actually unhealthy.
//   2. Without it, opening the API's own URL returns a bare 404, which reads as a broken deploy
//      to anyone who clicks it - including whoever is debugging this in six months.
//
// Deliberately says nothing else. No version, no database check, no counts: a health endpoint
// that queries the database reports the DATABASE's health, so a slow query would take the whole
// service down on a platform that restarts anything failing its check. "The process is up and
// serving HTTP" is the only claim this makes, and the only one it can make cheaply.
//
// Anonymous without needing [AllowAnonymous]: authorization here is opt-in per endpoint, and
// there is no fallback policy, so an endpoint with no requirement is simply open.
app.MapGet("/", () => Results.Ok(new { service = "TakeawayOS API", status = "ok" }));

app.Run();

// Top-level statements above compile into an auto-generated, internal 'Program' class. 
// 'partial' = more of this same class is declared here
public partial class Program { } // for factory to reference the Program class and start the app in-memory

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