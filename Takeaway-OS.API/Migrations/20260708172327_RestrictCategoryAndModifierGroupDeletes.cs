using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Takeaway_OS.API.Migrations
{
    /// <inheritdoc />
    public partial class RestrictCategoryAndModifierGroupDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuItemModifierGroups_ModifierGroups_ModifierGroupId",
                table: "MenuItemModifierGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Categories_CategoryId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ModifierOptions_ModifierGroups_ModifierGroupId",
                table: "ModifierOptions");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItemModifierGroups_ModifierGroups_ModifierGroupId",
                table: "MenuItemModifierGroups",
                column: "ModifierGroupId",
                principalTable: "ModifierGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Categories_CategoryId",
                table: "MenuItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModifierOptions_ModifierGroups_ModifierGroupId",
                table: "ModifierOptions",
                column: "ModifierGroupId",
                principalTable: "ModifierGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuItemModifierGroups_ModifierGroups_ModifierGroupId",
                table: "MenuItemModifierGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Categories_CategoryId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ModifierOptions_ModifierGroups_ModifierGroupId",
                table: "ModifierOptions");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItemModifierGroups_ModifierGroups_ModifierGroupId",
                table: "MenuItemModifierGroups",
                column: "ModifierGroupId",
                principalTable: "ModifierGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Categories_CategoryId",
                table: "MenuItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ModifierOptions_ModifierGroups_ModifierGroupId",
                table: "ModifierOptions",
                column: "ModifierGroupId",
                principalTable: "ModifierGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
