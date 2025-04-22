using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class LocationCustomField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsArchived",
                table: "Locations",
                newName: "IsDeactivated");

            migrationBuilder.CreateTable(
                name: "LocationCustomFields",
                columns: table => new
                {
                    LocationId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    CustomFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationCustomFields", x => new { x.LocationId, x.CompanyId, x.CustomFieldId });
                    table.ForeignKey(
                        name: "FK_LocationCustomFields_CustomFields_CustomFieldId",
                        column: x => x.CustomFieldId,
                        principalTable: "CustomFields",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationCustomFields_CustomFieldId",
                table: "LocationCustomFields",
                column: "CustomFieldId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationCustomFields");

            migrationBuilder.RenameColumn(
                name: "IsDeactivated",
                table: "Locations",
                newName: "IsArchived");
        }
    }
}
