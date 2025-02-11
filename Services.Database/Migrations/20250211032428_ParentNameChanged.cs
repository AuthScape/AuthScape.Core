using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class ParentNameChanged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "ProductCardCategories");

            migrationBuilder.AddColumn<string>(
                name: "ParentName",
                table: "ProductCardCategories",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentName",
                table: "ProductCardCategories");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "ProductCardCategories",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
