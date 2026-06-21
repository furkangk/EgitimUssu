using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Settings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "settings");

            migrationBuilder.CreateTable(
                name: "module_states",
                schema: "settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_states", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                schema: "settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PushNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpcomingLessonReminderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HomeworkReminderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PaymentReminderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WeeklySummaryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ShareStudyDataWithTeacher = table.Column<bool>(type: "boolean", nullable: false),
                    ShareStudyDataWithParent = table.Column<bool>(type: "boolean", nullable: false),
                    PrivacyLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SessionTerminationPolicy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastUpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc",
                schema: "settings",
                table: "outbox_messages",
                column: "ProcessedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_user_settings_UserId",
                schema: "settings",
                table: "user_settings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_states",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "user_settings",
                schema: "settings");
        }
    }
}
