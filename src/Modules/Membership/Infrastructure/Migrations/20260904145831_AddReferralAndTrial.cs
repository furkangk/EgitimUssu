using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Membership.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralAndTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PlanId",
                schema: "membership",
                table: "user_subscriptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "referral_codes",
                schema: "membership",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RedemptionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "referral_redemptions",
                schema: "membership",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RedeemerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardMonths = table.Column<int>(type: "integer", nullable: false),
                    RedeemedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_redemptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_referral_codes_Code",
                schema: "membership",
                table: "referral_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_referral_codes_OwnerUserId",
                schema: "membership",
                table: "referral_codes",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_referral_redemptions_RedeemerUserId",
                schema: "membership",
                table: "referral_redemptions",
                column: "RedeemerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_referral_redemptions_ReferrerUserId",
                schema: "membership",
                table: "referral_redemptions",
                column: "ReferrerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral_codes",
                schema: "membership");

            migrationBuilder.DropTable(
                name: "referral_redemptions",
                schema: "membership");

            migrationBuilder.AlterColumn<Guid>(
                name: "PlanId",
                schema: "membership",
                table: "user_subscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
