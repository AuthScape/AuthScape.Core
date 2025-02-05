using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRecursionToPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Recursion",
                table: "Pages",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Recursion",
                table: "Pages");
        }
    }
}
