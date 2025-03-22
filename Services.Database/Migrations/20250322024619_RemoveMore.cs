using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductCardAndCardFieldMapping_ProductCardFields_ProductFieldId",
                table: "ProductCardAndCardFieldMapping");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductCardFields_ProductCardCategories_ProductCategoryId",
                table: "ProductCardFields");

            migrationBuilder.DropIndex(
                name: "IX_ProductCardFields_ProductCategoryId",
                table: "ProductCardFields");

            migrationBuilder.DropIndex(
                name: "IX_ProductCardAndCardFieldMapping_ProductFieldId",
                table: "ProductCardAndCardFieldMapping");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProductCardFields_ProductCategoryId",
                table: "ProductCardFields",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCardAndCardFieldMapping_ProductFieldId",
                table: "ProductCardAndCardFieldMapping",
                column: "ProductFieldId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCardAndCardFieldMapping_ProductCardFields_ProductFieldId",
                table: "ProductCardAndCardFieldMapping",
                column: "ProductFieldId",
                principalTable: "ProductCardFields",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCardFields_ProductCardCategories_ProductCategoryId",
                table: "ProductCardFields",
                column: "ProductCategoryId",
                principalTable: "ProductCardCategories",
                principalColumn: "Id");
        }
    }
}
