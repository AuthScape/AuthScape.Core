using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class PhotoRemovedAndAnalyticsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Photo",
                table: "ProductCards");

            migrationBuilder.CreateTable(
                name: "AnalyticsMarketplaceImpressionsClicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    ProductOrServiceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JSONProductList = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsMarketplaceImpressionsClicks", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsMarketplaceImpressionsClicks");

            migrationBuilder.AddColumn<string>(
                name: "Photo",
                table: "ProductCards",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
