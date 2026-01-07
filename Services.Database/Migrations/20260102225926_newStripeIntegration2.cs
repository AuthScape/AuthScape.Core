using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Services.Database.Migrations
{
    /// <inheritdoc />
    public partial class newStripeIntegration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromoCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StripeCouponId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StripePromotionCodeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastStripeSyncAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    DurationInMonths = table.Column<int>(type: "int", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "int", nullable: true),
                    TimesRedeemed = table.Column<int>(type: "int", nullable: false),
                    MaxRedemptionsPerCustomer = table.Column<int>(type: "int", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    RestrictedToUserId = table.Column<long>(type: "bigint", nullable: true),
                    RestrictedToCompanyId = table.Column<long>(type: "bigint", nullable: true),
                    RestrictedToLocationId = table.Column<long>(type: "bigint", nullable: true),
                    AppliesTo = table.Column<int>(type: "int", nullable: false),
                    ApplicablePlanIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApplicableProductIds = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExtendsTrialDays = table.Column<bool>(type: "bit", nullable: false),
                    AdditionalTrialDays = table.Column<int>(type: "int", nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_Code",
                table: "PromoCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_ExpiresAt",
                table: "PromoCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_IsActive",
                table: "PromoCodes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_Scope",
                table: "PromoCodes",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_StripeCouponId",
                table: "PromoCodes",
                column: "StripeCouponId",
                filter: "[StripeCouponId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCodes_StripePromotionCodeId",
                table: "PromoCodes",
                column: "StripePromotionCodeId",
                filter: "[StripePromotionCodeId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromoCodes");
        }
    }
}
