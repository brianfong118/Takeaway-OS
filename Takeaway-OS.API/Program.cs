using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.Services; 

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ICategoryService, CategoryService>(); 
builder.Services.AddScoped<IMenuItemService, MenuItemService>();
builder.Services.AddScoped<IDriverService, DriverService>();
// Register the XService with the DI container, so that it can be injected into controllers or other services that require it.




var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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