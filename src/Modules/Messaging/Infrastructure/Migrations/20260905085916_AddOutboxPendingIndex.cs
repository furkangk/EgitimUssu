using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Messaging.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxPendingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "messaging",
                table: "outbox_messages",
                columns: new[] { "NextAttemptUtc", "OccurredOnUtc" },
                filter: "\"ProcessedOnUtc\" IS NULL AND \"DeadLetteredOnUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_ProcessedOnUtc",
                schema: "messaging",
                table: "inbox_messages",
                column: "ProcessedOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_pending",
                schema: "messaging",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_inbox_messages_ProcessedOnUtc",
                schema: "messaging",
                table: "inbox_messages");
        }
    }
}
