using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class CategoryTypeAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCardIndexType",
                table: "ProductCardFields");

            migrationBuilder.AddColumn<int>(
                name: "ProductCardCategoryType",
                table: "ProductCardCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCardCategoryType",
                table: "ProductCardCategories");

            migrationBuilder.AddColumn<int>(
                name: "ProductCardIndexType",
                table: "ProductCardFields",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
