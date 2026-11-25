using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Takeaway_OS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Looks like it adds a column, but it doesn't touch the table. "xmin" is a system column
            // every Postgres row already has, and the Npgsql provider generates NO SQL for system
            // columns (checked with `dotnet ef migrations script`: the only statement is the
            // __EFMigrationsHistory insert). The migration exists so EF's model snapshot records that
            // Order.Version maps to xmin as a concurrency token; without it the next migration would
            // try to "add" it again.
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Orders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Orders");
        }
    }
}
