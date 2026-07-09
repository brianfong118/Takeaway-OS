using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Data;

// IdentityDbContext<ApplicationUser, IdentityRole<int>, int> adds the Identity tables
// (Users, Roles, UserRoles, UserClaims, UserLogins, UserTokens, RoleClaims) on top of
// everything DbContext already gives us - same database, same context, one set of migrations.
public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OrderItemModifier> OrderItemModifiers { get; set; }
    public DbSet<Driver> Drivers { get; set; }
    public DbSet<ModifierGroup> ModifierGroups { get; set; }
    public DbSet<ModifierOption> ModifierOptions { get; set; }
    public DbSet<MenuItemModifierGroup> MenuItemModifierGroups { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Address> Addresses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Must run first - this is what actually builds the AspNetUsers/AspNetRoles/etc. tables.
        base.OnModelCreating(modelBuilder);

        // Driver/Customer are 1:1 with ApplicationUser. A unique index on the FK is what
        // actually enforces "one login, one profile" at the database level - without it,
        // EF would happily let two Drivers point at the same ApplicationUserId.
        modelBuilder.Entity<Driver>()
            .HasOne(d => d.ApplicationUser)
            .WithOne()
            .HasForeignKey<Driver>(d => d.ApplicationUserId);
        modelBuilder.Entity<Driver>()
            .HasIndex(d => d.ApplicationUserId)
            .IsUnique();

        modelBuilder.Entity<Customer>()
            .HasOne(c => c.ApplicationUser)
            .WithOne()
            .HasForeignKey<Customer>(c => c.ApplicationUserId);
        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.ApplicationUserId)
            .IsUnique();

        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.OrderType)
            .HasConversion<string>();


        // Both FKs below are required (non-nullable)
        // Therefore EF Core's default would be Cascade 
        // Deleting a Category would silently delete every MenuItem in it
        // Deleting a ModifierGroup would silently delete every ModifierOption and every MenuItemModifierGroup link to it
        // Restrict instead: the delete is blocked at the database level while
        // dependents exist and the service/controller turn that into a 409 Conflict.
        modelBuilder.Entity<MenuItem>()
            .HasOne(m => m.Category)
            .WithMany(c => c.MenuItems)
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ModifierOption>()
            .HasOne(o => o.ModifierGroup)
            .WithMany(g => g.ModifierOptions)
            .HasForeignKey(o => o.ModifierGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MenuItemModifierGroup>()
            .HasOne(l => l.ModifierGroup)
            .WithMany(g => g.MenuItemModifierGroups)
            .HasForeignKey(l => l.ModifierGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // MenuItemModifierGroup's other FK (-> MenuItem) stays Cascade: deleting a MenuItem
        // should take its own links with it 
    }
}