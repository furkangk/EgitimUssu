using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Parents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxMessagesReplaceProcessed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_integration_events",
                schema: "parents");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "parents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Handler = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => new { x.EventId, x.Handler });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "parents");

            migrationBuilder.CreateTable(
                name: "processed_integration_events",
                schema: "parents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_integration_events", x => x.Id);
                });
        }
    }
}
