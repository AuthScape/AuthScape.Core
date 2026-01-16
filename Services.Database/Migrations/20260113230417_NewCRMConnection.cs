using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class NewCRMConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmConnections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokenExpiry = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebhookSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnvironmentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SyncDirection = table.Column<int>(type: "int", nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastSyncAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSyncError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Updated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmConnections_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CrmEntityMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrmConnectionId = table.Column<long>(type: "bigint", nullable: false),
                    CrmEntityName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CrmEntityDisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AuthScapeEntityType = table.Column<int>(type: "int", nullable: false),
                    SyncDirection = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CrmFilterExpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CrmPrimaryKeyField = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CrmModifiedDateField = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmEntityMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmEntityMappings_CrmConnections_CrmConnectionId",
                        column: x => x.CrmConnectionId,
                        principalTable: "CrmConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrmExternalIds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrmConnectionId = table.Column<long>(type: "bigint", nullable: false),
                    AuthScapeEntityType = table.Column<int>(type: "int", nullable: false),
                    AuthScapeEntityId = table.Column<long>(type: "bigint", nullable: false),
                    CrmEntityName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CrmEntityId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSyncDirection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LastSyncHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmExternalIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmExternalIds_CrmConnections_CrmConnectionId",
                        column: x => x.CrmConnectionId,
                        principalTable: "CrmConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrmFieldMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrmEntityMappingId = table.Column<long>(type: "bigint", nullable: false),
                    AuthScapeField = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CrmField = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SyncDirection = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    TransformationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TransformationConfig = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmFieldMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmFieldMappings_CrmEntityMappings_CrmEntityMappingId",
                        column: x => x.CrmEntityMappingId,
                        principalTable: "CrmEntityMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrmSyncLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CrmConnectionId = table.Column<long>(type: "bigint", nullable: false),
                    CrmEntityMappingId = table.Column<long>(type: "bigint", nullable: true),
                    AuthScapeEntityType = table.Column<int>(type: "int", nullable: true),
                    AuthScapeEntityId = table.Column<long>(type: "bigint", nullable: true),
                    CrmEntityName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CrmEntityId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordsProcessed = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmSyncLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmSyncLogs_CrmConnections_CrmConnectionId",
                        column: x => x.CrmConnectionId,
                        principalTable: "CrmConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrmSyncLogs_CrmEntityMappings_CrmEntityMappingId",
                        column: x => x.CrmEntityMappingId,
                        principalTable: "CrmEntityMappings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmConnections_CompanyId",
                table: "CrmConnections",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmConnections_IsEnabled",
                table: "CrmConnections",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_CrmConnections_Provider",
                table: "CrmConnections",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_CrmEntityMappings_CrmConnectionId",
                table: "CrmEntityMappings",
                column: "CrmConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmEntityMappings_CrmConnectionId_CrmEntityName",
                table: "CrmEntityMappings",
                columns: new[] { "CrmConnectionId", "CrmEntityName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmExternalIds_CrmConnectionId_AuthScapeEntityType_AuthScapeEntityId",
                table: "CrmExternalIds",
                columns: new[] { "CrmConnectionId", "AuthScapeEntityType", "AuthScapeEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmExternalIds_CrmConnectionId_CrmEntityName_CrmEntityId",
                table: "CrmExternalIds",
                columns: new[] { "CrmConnectionId", "CrmEntityName", "CrmEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmFieldMappings_CrmEntityMappingId",
                table: "CrmFieldMappings",
                column: "CrmEntityMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmFieldMappings_CrmEntityMappingId_AuthScapeField_CrmField",
                table: "CrmFieldMappings",
                columns: new[] { "CrmEntityMappingId", "AuthScapeField", "CrmField" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmSyncLogs_CrmConnectionId",
                table: "CrmSyncLogs",
                column: "CrmConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmSyncLogs_CrmConnectionId_SyncedAt",
                table: "CrmSyncLogs",
                columns: new[] { "CrmConnectionId", "SyncedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmSyncLogs_CrmEntityMappingId",
                table: "CrmSyncLogs",
                column: "CrmEntityMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmSyncLogs_Status",
                table: "CrmSyncLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CrmSyncLogs_SyncedAt",
                table: "CrmSyncLogs",
                column: "SyncedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmExternalIds");

            migrationBuilder.DropTable(
                name: "CrmFieldMappings");

            migrationBuilder.DropTable(
                name: "CrmSyncLogs");

            migrationBuilder.DropTable(
                name: "CrmEntityMappings");

            migrationBuilder.DropTable(
                name: "CrmConnections");
        }
    }
}
