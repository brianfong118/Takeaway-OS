using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Takeaway_OS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPublicToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicToken",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Hand-added, and the migration does not work without it.
            // AddColumn above gives EVERY pre-existing row the same all-zeros default, so the unique
            // index below would fail on duplicate keys the moment there is more than one old order.
            // gen_random_uuid() is evaluated by Postgres per row, so each old order gets a distinct
            // real token -> and one nobody can derive from the zeros default either.
            // Scoped to rows still holding the default, so a re-run can't reissue a live token.
            migrationBuilder.Sql(
                @"UPDATE ""Orders""
                  SET ""PublicToken"" = gen_random_uuid()
                  WHERE ""PublicToken"" = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PublicToken",
                table: "Orders",
                column: "PublicToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_PublicToken",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PublicToken",
                table: "Orders");
        }
    }
}
