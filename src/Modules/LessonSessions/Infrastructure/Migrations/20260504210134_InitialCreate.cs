using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.LessonSessions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lesson_sessions");

            migrationBuilder.CreateTable(
                name: "lesson_sessions",
                schema: "lesson_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PlannedStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    AttendanceStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TopicTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CoveredContent = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TeacherNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "module_states",
                schema: "lesson_sessions",
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
                schema: "lesson_sessions",
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

            migrationBuilder.CreateIndex(
                name: "IX_lesson_sessions_LessonScheduleId",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                column: "LessonScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_sessions_StudentId_PlannedStartAtUtc",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                columns: new[] { "StudentId", "PlannedStartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_sessions_TeacherUserId_PlannedStartAtUtc",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                columns: new[] { "TeacherUserId", "PlannedStartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc",
                schema: "lesson_sessions",
                table: "outbox_messages",
                column: "ProcessedOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_sessions",
                schema: "lesson_sessions");

            migrationBuilder.DropTable(
                name: "module_states",
                schema: "lesson_sessions");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "lesson_sessions");
        }
    }
}
