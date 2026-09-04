using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.LessonSessions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionLifecycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingUrl",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordingUrl",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "lesson_sessions",
                table: "lesson_sessions");

            migrationBuilder.DropColumn(
                name: "MeetingUrl",
                schema: "lesson_sessions",
                table: "lesson_sessions");

            migrationBuilder.DropColumn(
                name: "RecordingUrl",
                schema: "lesson_sessions",
                table: "lesson_sessions");
        }
    }
}
