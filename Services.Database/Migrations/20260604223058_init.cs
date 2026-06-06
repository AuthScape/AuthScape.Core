using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountLinkAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppUserId = table.Column<long>(type: "bigint", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalUserId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    EmailVerifiedByProvider = table.Column<bool>(type: "bit", nullable: false),
                    PolicyApplied = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    VerificationTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLinkAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Started = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Ended = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Deleted = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Duration = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Device = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Logo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "Loggings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Browser = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    Uri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RouteAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RouteController = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Runtime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OS = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Referer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InnerExceptionMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExceptionStackTrace = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InnerExceptionStackTrace = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loggings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationCategoryConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Modified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationCategoryConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    SettingTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThirdPartyAuthentications",
                columns: table => new
                {
                    ThirdPartyAuthenticationType = table.Column<int>(type: "int", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdditionalSettings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThirdPartyAuthentications", x => x.ThirdPartyAuthenticationType);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsConversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsConversions_AnalyticsSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AnalyticsSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Uri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Referrer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Deleted = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsEvents_AnalyticsSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AnalyticsSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsPageViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    Uri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Referrer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Deleted = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsPageViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsPageViews_AnalyticsSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AnalyticsSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InviteSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    EnableInviteToCompany = table.Column<bool>(type: "bit", nullable: false),
                    EnableInviteToLocation = table.Column<bool>(type: "bit", nullable: false),
                    AllowSettingPermissions = table.Column<bool>(type: "bit", nullable: false),
                    AllowSettingRoles = table.Column<bool>(type: "bit", nullable: false),
                    EnforceSamePermissions = table.Column<bool>(type: "bit", nullable: false),
                    EnforceSameRole = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InviteSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InviteSettings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lat = table.Column<float>(type: "real", nullable: true),
                    lng = table.Column<float>(type: "real", nullable: true),
                    IsDeactivated = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Modified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_NotificationCategoryConfigs_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "NotificationCategoryConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EmailSent = table.Column<bool>(type: "bit", nullable: false),
                    SmsSent = table.Column<bool>(type: "bit", nullable: false),
                    EmailSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SmsSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_NotificationCategoryConfigs_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "NotificationCategoryConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    locale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Archived = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastLoggedIn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PhotoUri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    WhenInviteSent = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Culture = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalProvider = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ExternalSub = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fido2Credentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CredentialId = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    PublicKey = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UserHandle = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                    CredType = table.Column<int>(type: "int", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AaGuid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeviceName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fido2Credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fido2Credentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    InvitedUserId = table.Column<long>(type: "bigint", nullable: false),
                    InviterId = table.Column<long>(type: "bigint", nullable: false),
                    InviteToken = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    LocationId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedRoles = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssignedPermissions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInvites_AspNetUsers_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserInvites_AspNetUsers_InviterId",
                        column: x => x.InviterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserInvites_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserInvites_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsConversions_SessionId",
                table: "AnalyticsConversions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsEvents_SessionId",
                table: "AnalyticsEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsPageViews_SessionId",
                table: "AnalyticsPageViews",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ExternalProvider_ExternalSub",
                table: "AspNetUsers",
                columns: new[] { "ExternalProvider", "ExternalSub" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_LocationId",
                table: "AspNetUsers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_Fido2Credentials_UserId",
                table: "Fido2Credentials",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InviteSettings_CompanyId",
                table: "InviteSettings",
                column: "CompanyId",
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CompanyId",
                table: "Locations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationCategoryConfigs_Name",
                table: "NotificationCategoryConfigs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_CategoryId",
                table: "NotificationPreferences",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_CategoryId",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CategoryId",
                table: "Notifications",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CompanyId",
                table: "Notifications",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Created",
                table: "Notifications",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_LocationId",
                table: "Notifications",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInvites_CompanyId",
                table: "UserInvites",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvites_InvitedUserId",
                table: "UserInvites",
                column: "InvitedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvites_InviterId",
                table: "UserInvites",
                column: "InviterId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvites_InviteToken",
                table: "UserInvites",
                column: "InviteToken");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvites_LocationId",
                table: "UserInvites",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvites_Status",
                table: "UserInvites",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountLinkAuditLogs");

            migrationBuilder.DropTable(
                name: "AnalyticsConversions");

            migrationBuilder.DropTable(
                name: "AnalyticsEvents");

            migrationBuilder.DropTable(
                name: "AnalyticsPageViews");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "ErrorGroups");

            migrationBuilder.DropTable(
                name: "ErrorLogs");

            migrationBuilder.DropTable(
                name: "ErrorTrackingSettings");

            migrationBuilder.DropTable(
                name: "Fido2Credentials");

            migrationBuilder.DropTable(
                name: "InviteSettings");

            migrationBuilder.DropTable(
                name: "Loggings");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "ThirdPartyAuthentications");

            migrationBuilder.DropTable(
                name: "UserInvites");

            migrationBuilder.DropTable(
                name: "AnalyticsSessions");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "NotificationCategoryConfigs");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
