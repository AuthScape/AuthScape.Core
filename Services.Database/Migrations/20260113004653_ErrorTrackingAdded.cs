using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class ErrorTrackingAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErrorGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    ErrorSignature = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SampleStackTrace = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Environment = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    ErrorGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestBodyTruncated = table.Column<bool>(type: "bit", nullable: false),
                    ResponseBodyTruncated = table.Column<bool>(type: "bit", nullable: false),
                    QueryString = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestHeaders = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ResponseHeaders = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Browser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BrowserVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OperatingSystem = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AnalyticsSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    DatabaseProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApplicationVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    MemoryUsageMB = table.Column<long>(type: "bigint", nullable: true),
                    ThreadCount = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ComponentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AdditionalMetadata = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErrorTrackingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Track400Errors = table.Column<bool>(type: "bit", nullable: false),
                    Track401Errors = table.Column<bool>(type: "bit", nullable: false),
                    Track403Errors = table.Column<bool>(type: "bit", nullable: false),
                    Track404Errors = table.Column<bool>(type: "bit", nullable: false),
                    Track500Errors = table.Column<bool>(type: "bit", nullable: false),
                    Track502Errors = table.Column<bool>(type: "bit", nullable: false),
                    Track503Errors = table.Column<bool>(type: "bit", nullable: false),
                    CustomStatusCodes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnableFrontendTracking = table.Column<bool>(type: "bit", nullable: false),
                    FrontendBatchIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    RetentionPeriodDays = table.Column<int>(type: "int", nullable: false),
                    MaxBodySizeKB = table.Column<int>(type: "int", nullable: false),
                    CaptureRequestBody = table.Column<bool>(type: "bit", nullable: false),
                    CaptureResponseBody = table.Column<bool>(type: "bit", nullable: false),
                    CaptureHeaders = table.Column<bool>(type: "bit", nullable: false),
                    CapturePerformanceMetrics = table.Column<bool>(type: "bit", nullable: false),
                    LinkToAnalyticsSessions = table.Column<bool>(type: "bit", nullable: false),
                    AutoRedactSensitiveData = table.Column<bool>(type: "bit", nullable: false),
                    CustomRedactionFields = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EnableErrorNotifications = table.Column<bool>(type: "bit", nullable: false),
                    NotifyForStatusCodes = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Modified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorTrackingSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroups_Environment",
                table: "ErrorGroups",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroups_ErrorSignature",
                table: "ErrorGroups",
                column: "ErrorSignature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroups_FirstSeen",
                table: "ErrorGroups",
                column: "FirstSeen");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroups_IsResolved",
                table: "ErrorGroups",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroups_IsResolved_LastSeen",
                table: "ErrorGroups",
                columns: new[] { "IsResolved", "LastSeen" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroups_LastSeen",
                table: "ErrorGroups",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroups_Source",
                table: "ErrorGroups",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorGroups_StatusCode",
                table: "ErrorGroups",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_AnalyticsSessionId",
                table: "ErrorLogs",
                column: "AnalyticsSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_Created",
                table: "ErrorLogs",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_Environment",
                table: "ErrorLogs",
                column: "Environment");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorGroupId",
                table: "ErrorLogs",
                column: "ErrorGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_IsResolved",
                table: "ErrorLogs",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_IsResolved_Created",
                table: "ErrorLogs",
                columns: new[] { "IsResolved", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_Source",
                table: "ErrorLogs",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_Source_StatusCode_Created",
                table: "ErrorLogs",
                columns: new[] { "Source", "StatusCode", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_StatusCode",
                table: "ErrorLogs",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_UserId",
                table: "ErrorLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorGroups");

            migrationBuilder.DropTable(
                name: "ErrorLogs");

            migrationBuilder.DropTable(
                name: "ErrorTrackingSettings");
        }
    }
}
