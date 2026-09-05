using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_deletion_requests",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CancelledOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_deletion_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_Status_ScheduledForUtc",
                schema: "identity",
                table: "account_deletion_requests",
                columns: new[] { "Status", "ScheduledForUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_UserAccountId_Status",
                schema: "identity",
                table: "account_deletion_requests",
                columns: new[] { "UserAccountId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_deletion_requests",
                schema: "identity");
        }
    }
}
