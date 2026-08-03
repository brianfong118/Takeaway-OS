using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Takeaway_OS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "RestaurantSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "RestaurantSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "DeliveryFee",
                value: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "Orders");
        }
    }
}
