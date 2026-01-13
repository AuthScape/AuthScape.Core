using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class notificationCategoryAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationCategoryConfigs_EnumValue",
                table: "NotificationCategoryConfigs");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EnumValue",
                table: "NotificationCategoryConfigs");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "NotificationPreferences",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationPreferences_UserId_Category",
                table: "NotificationPreferences",
                newName: "IX_NotificationPreferences_UserId_CategoryId");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CategoryId",
                table: "Notifications",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_CategoryId",
                table: "NotificationPreferences",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationPreferences_NotificationCategoryConfigs_CategoryId",
                table: "NotificationPreferences",
                column: "CategoryId",
                principalTable: "NotificationCategoryConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_NotificationCategoryConfigs_CategoryId",
                table: "Notifications",
                column: "CategoryId",
                principalTable: "NotificationCategoryConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationPreferences_NotificationCategoryConfigs_CategoryId",
                table: "NotificationPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_NotificationCategoryConfigs_CategoryId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CategoryId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPreferences_CategoryId",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "NotificationPreferences",
                newName: "Category");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationPreferences_UserId_CategoryId",
                table: "NotificationPreferences",
                newName: "IX_NotificationPreferences_UserId_Category");

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EnumValue",
                table: "NotificationCategoryConfigs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationCategoryConfigs_EnumValue",
                table: "NotificationCategoryConfigs",
                column: "EnumValue",
                unique: true);
        }
    }
}
