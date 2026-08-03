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
    public DbSet<OpeningHours> OpeningHours { get; set; }
    public DbSet<RestaurantSettings> RestaurantSettings { get; set; }
    public DbSet<DeliveryArea> DeliveryAreas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Must run first - this is what actually builds the AspNetUsers/AspNetRoles/etc. tables.
        base.OnModelCreating(modelBuilder);

        // Driver/Customer are 1:1 with ApplicationUser
        // Unique index on the FK enforces "one login, one profile" at the database level,
        // or else EF would let two Drivers point at the same ApplicationUserId.
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

        // Orders.CustomerId is nullable (guest checkout), so this is an optional relationship:
        // SetNull, not Restrict: an order is a financial record and must outlive the account that placed it 
        // Restrict would make a Customer with any order history undeletable; 
        // Cascade would delete real orders along with the account. 
        // SetNull keeps the order row and just detaches it, which degrades it to exactly a guest order 
        // name/phone/address snapshot above is still there, so it stays a complete record.

        // EF default for an *optional* relationship is ClientSetNull (null the FK only on entities already loaded in memory)
        // which leaves the database itself with no ON DELETE rule. 
        // Stating SetNull puts the real ON DELETE SET NULL into the Postgres schema, so a direct DELETE of a driver row can't orphan an order.

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany() // no navigation property on Customer, because nothing needs to walk Customer -> Orders in memory; order history is a query, not a loaded collection
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Order history -> FK is the filter column on every such query - without this index that's a full table scan.
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.CustomerId);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Driver)
            .WithMany() // no navigation collection on Driver -> a driver's order list is a query, not a loaded set (same call as Customer)
            .HasForeignKey(o => o.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.DriverId);

        // Two jobs, which is why it's unique and not just an index.
        // Index: the guest confirmation lookup filters on this column and nothing else, so without
        //   it every guest page load is a full scan of the Orders table.
        // Unique: makes the database refuse a second order carrying the same token, so the lookup
        //   provably matches at most one order. Guid.NewGuid() collisions are already effectively
        //   impossible -> this is the guarantee that a bug or a manual UPDATE can't create one either.
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.PublicToken)
            .IsUnique();

        // Readable straight from a psql query, and safe if the enum's numbers ever shift.
        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.OrderType)
            .HasConversion<string>();

        modelBuilder.Entity<OpeningHours>()
            .Property(oh => oh.DayOfWeek)
            .HasConversion<string>();

        // Unique, for the same two-jobs reason as Order.PublicToken above.
        // Index: IsAllowedAsync filters on this column and nothing else, and it runs on the
        //   hot path of every delivery order.
        // Unique: the Owner adding "E1" twice is a real slip, and this makes the database
        //   refuse it rather than relying on the service remembering to check first. It only
        //   works because OutwardCode is normalised (uppercase, no spaces) before it is
        //   stored - otherwise "e1" and "E1" would be two distinct rows to Postgres.
        modelBuilder.Entity<DeliveryArea>()
            .HasIndex(d => d.OutwardCode)
            .IsUnique();

        // HasData seeds the single settings row as part of the migration itself
        // So a fresh database always "open" rather than empty table (service has to null-check on every order)
        // Id is hardcoded because it's a singleton -> nothing ever inserts a second row.
        // Models.RestaurantSettings, fully qualified: inside this class the DbSet property named
        // RestaurantSettings shadows the type of the same name, so the bare name won't resolve.
        modelBuilder.Entity<RestaurantSettings>().HasData(new RestaurantSettings
        {
            Id = Models.RestaurantSettings.SingletonId,
            IsTemporarilyClosed = false,
            ClosureReason = string.Empty,
            // 0, not a realistic price: a real fee is the owner's data, so it gets typed
            // into the settings UI rather than baked into a migration.
            DeliveryFee = 0m
        });


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