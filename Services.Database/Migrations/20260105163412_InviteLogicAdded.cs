using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class InviteLogicAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "IX_InviteSettings_CompanyId",
                table: "InviteSettings",
                column: "CompanyId",
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

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
                name: "InviteSettings");

            migrationBuilder.DropTable(
                name: "UserInvites");
        }
    }
}
