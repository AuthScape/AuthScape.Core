using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class RedirectTrafficUpdated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RedirectWWWTraffic",
                table: "DnsRecords",
                newName: "RedirectTrafficToCanonical");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RedirectTrafficToCanonical",
                table: "DnsRecords",
                newName: "RedirectWWWTraffic");
        }
    }
}
