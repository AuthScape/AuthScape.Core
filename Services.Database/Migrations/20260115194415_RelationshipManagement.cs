using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class RelationshipManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmRelationshipMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrmEntityMappingId = table.Column<long>(type: "bigint", nullable: false),
                    AuthScapeField = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RelatedAuthScapeEntityType = table.Column<int>(type: "int", nullable: false),
                    CrmLookupField = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CrmRelatedEntityName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SyncDirection = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AutoCreateRelated = table.Column<bool>(type: "bit", nullable: false),
                    SyncNullValues = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmRelationshipMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmRelationshipMappings_CrmEntityMappings_CrmEntityMappingId",
                        column: x => x.CrmEntityMappingId,
                        principalTable: "CrmEntityMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmRelationshipMappings_CrmEntityMappingId",
                table: "CrmRelationshipMappings",
                column: "CrmEntityMappingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmRelationshipMappings");
        }
    }
}
