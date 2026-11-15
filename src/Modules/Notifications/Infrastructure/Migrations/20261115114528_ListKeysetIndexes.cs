using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ListKeysetIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_unread_keyset",
                schema: "notifications",
                table: "user_notifications",
                columns: new[] { "UserId", "CreatedOnUtc", "Id" },
                filter: "\"ReadOnUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_UserId_CreatedOnUtc_Id",
                schema: "notifications",
                table: "user_notifications",
                columns: new[] { "UserId", "CreatedOnUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_notifications_unread_keyset",
                schema: "notifications",
                table: "user_notifications");

            migrationBuilder.DropIndex(
                name: "IX_user_notifications_UserId_CreatedOnUtc_Id",
                schema: "notifications",
                table: "user_notifications");
        }
    }
}
