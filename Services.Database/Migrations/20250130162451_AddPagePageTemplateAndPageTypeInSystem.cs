using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPagePageTemplateAndPageTypeInSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CssData",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "HtmlData",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "PageType",
                table: "Pages");

            migrationBuilder.RenameColumn(
                name: "MetaDescription",
                table: "Pages",
                newName: "Content");

            migrationBuilder.AddColumn<long>(
                name: "PageTemplateId",
                table: "Pages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "PageTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PageTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PageTypeId = table.Column<long>(type: "bigint", nullable: false),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Archived = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageTemplates_PageTypes_PageTypeId",
                        column: x => x.PageTypeId,
                        principalTable: "PageTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pages_PageTemplateId",
                table: "Pages",
                column: "PageTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PageTemplates_PageTypeId",
                table: "PageTemplates",
                column: "PageTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_PageTemplates_PageTemplateId",
                table: "Pages",
                column: "PageTemplateId",
                principalTable: "PageTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pages_PageTemplates_PageTemplateId",
                table: "Pages");

            migrationBuilder.DropTable(
                name: "PageTemplates");

            migrationBuilder.DropTable(
                name: "PageTypes");

            migrationBuilder.DropIndex(
                name: "IX_Pages_PageTemplateId",
                table: "Pages");

            migrationBuilder.DropColumn(
                name: "PageTemplateId",
                table: "Pages");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Pages",
                newName: "MetaDescription");

            migrationBuilder.AddColumn<string>(
                name: "CssData",
                table: "Pages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HtmlData",
                table: "Pages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageType",
                table: "Pages",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
