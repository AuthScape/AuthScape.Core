using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigAndDataInPageTemplateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "PageTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "PageTemplates");
        }
    }
}
