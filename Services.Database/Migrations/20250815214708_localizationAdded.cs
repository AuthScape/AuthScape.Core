using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class localizationAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Culture",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Culture",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "AspNetUsers");
        }
    }
}
