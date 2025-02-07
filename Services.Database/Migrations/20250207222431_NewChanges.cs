using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class NewChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProductOrServiceId",
                table: "AnalyticsMarketplaceImpressionsClicks",
                newName: "ProductOrServiceClicked");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProductOrServiceClicked",
                table: "AnalyticsMarketplaceImpressionsClicks",
                newName: "ProductOrServiceId");
        }
    }
}
