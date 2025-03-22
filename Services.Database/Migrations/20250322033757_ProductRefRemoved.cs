using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class ProductRefRemoved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductCardAndCardFieldMapping_ProductCards_ProductId",
                table: "ProductCardAndCardFieldMapping");

            migrationBuilder.DropIndex(
                name: "IX_ProductCardAndCardFieldMapping_ProductId",
                table: "ProductCardAndCardFieldMapping");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProductCardAndCardFieldMapping_ProductId",
                table: "ProductCardAndCardFieldMapping",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCardAndCardFieldMapping_ProductCards_ProductId",
                table: "ProductCardAndCardFieldMapping",
                column: "ProductId",
                principalTable: "ProductCards",
                principalColumn: "Id");
        }
    }
}
