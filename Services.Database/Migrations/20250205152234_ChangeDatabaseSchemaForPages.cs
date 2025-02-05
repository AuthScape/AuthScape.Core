using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDatabaseSchemaForPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pages_PageTemplates_PageTemplateId",
                table: "Pages");

            migrationBuilder.DropTable(
                name: "PageTemplates");

            migrationBuilder.RenameColumn(
                name: "PageTemplateId",
                table: "Pages",
                newName: "PageTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Pages_PageTemplateId",
                table: "Pages",
                newName: "IX_Pages_PageTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pages_PageTypes_PageTypeId",
                table: "Pages",
                column: "PageTypeId",
                principalTable: "PageTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pages_PageTypes_PageTypeId",
                table: "Pages");

            migrationBuilder.RenameColumn(
                name: "PageTypeId",
                table: "Pages",
                newName: "PageTemplateId");

            migrationBuilder.RenameIndex(
                name: "IX_Pages_PageTypeId",
                table: "Pages",
                newName: "IX_Pages_PageTemplateId");

            migrationBuilder.CreateTable(
                name: "PageTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageTypeId = table.Column<long>(type: "bigint", nullable: false),
                    Archived = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Config = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
    }
}
