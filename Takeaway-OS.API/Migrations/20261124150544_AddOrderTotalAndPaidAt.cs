using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Takeaway_OS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTotalAndPaidAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // Hand-added. AddColumn above gives every existing order a Total of 0, which would show
            // every past order as free. This fills each one in with what it was actually charged,
            // using the formula that was live when it was placed:
            //   sum(UnitPrice * Quantity) + sum(PriceDelta, once per selected option) + DeliveryFee
            // It must stay that PER-LINE formula even after ComputeTotal changes. Old orders were
            // charged under it, and the whole point of storing Total is that they keep that number.
            // COALESCE because SUM over zero rows is NULL in SQL, not 0 (an order with no modifiers).
            // PaidAt is deliberately left NULL: when an existing order was paid was never recorded,
            // and inventing a time would be worse than admitting there isn't one.
            migrationBuilder.Sql(
                @"UPDATE ""Orders"" o
                  SET ""Total"" =
                        COALESCE((SELECT SUM(oi.""UnitPrice"" * oi.""Quantity"")
                                  FROM ""OrderItems"" oi
                                  WHERE oi.""OrderId"" = o.""Id""), 0)
                      + COALESCE((SELECT SUM(m.""PriceDelta"")
                                  FROM ""OrderItemModifiers"" m
                                  JOIN ""OrderItems"" oi ON oi.""Id"" = m.""OrderItemId""
                                  WHERE oi.""OrderId"" = o.""Id""), 0)
                      + o.""DeliveryFee"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Orders");
        }
    }
}
