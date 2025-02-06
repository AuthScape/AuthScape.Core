using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class JSONFilterAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductReferenceId",
                table: "ProductCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JSONFilterSelected",
                table: "AnalyticsMarketplaceImpressionsClicks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductReferenceId",
                table: "ProductCards");

            migrationBuilder.DropColumn(
                name: "JSONFilterSelected",
                table: "AnalyticsMarketplaceImpressionsClicks");
        }
    }
}
