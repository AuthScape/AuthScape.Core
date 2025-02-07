using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class CompanyAndPlatformAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "ProductCards",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformId",
                table: "ProductCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "ProductCardFields",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformId",
                table: "ProductCardFields",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "ProductCardCategories",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformId",
                table: "ProductCardCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "ProductCardAndCardFieldMapping",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformId",
                table: "ProductCardAndCardFieldMapping",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ProductCards");

            migrationBuilder.DropColumn(
                name: "PlatformId",
                table: "ProductCards");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ProductCardFields");

            migrationBuilder.DropColumn(
                name: "PlatformId",
                table: "ProductCardFields");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ProductCardCategories");

            migrationBuilder.DropColumn(
                name: "PlatformId",
                table: "ProductCardCategories");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ProductCardAndCardFieldMapping");

            migrationBuilder.DropColumn(
                name: "PlatformId",
                table: "ProductCardAndCardFieldMapping");
        }
    }
}
