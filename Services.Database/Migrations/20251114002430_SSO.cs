using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class SSO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdditionalSettings",
                table: "ThirdPartyAuthentications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ThirdPartyAuthentications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "ThirdPartyAuthentications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "ThirdPartyAuthentications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Scopes",
                table: "ThirdPartyAuthentications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ThirdPartyAuthentications",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalSettings",
                table: "ThirdPartyAuthentications");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ThirdPartyAuthentications");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "ThirdPartyAuthentications");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "ThirdPartyAuthentications");

            migrationBuilder.DropColumn(
                name: "Scopes",
                table: "ThirdPartyAuthentications");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ThirdPartyAuthentications");
        }
    }
}
