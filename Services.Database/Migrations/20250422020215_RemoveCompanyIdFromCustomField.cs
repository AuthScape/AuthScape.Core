using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCompanyIdFromCustomField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LocationCustomFields",
                table: "LocationCustomFields");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "LocationCustomFields");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LocationCustomFields",
                table: "LocationCustomFields",
                columns: new[] { "LocationId", "CustomFieldId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LocationCustomFields",
                table: "LocationCustomFields");

            migrationBuilder.AddColumn<long>(
                name: "CompanyId",
                table: "LocationCustomFields",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_LocationCustomFields",
                table: "LocationCustomFields",
                columns: new[] { "LocationId", "CompanyId", "CustomFieldId" });
        }
    }
}
