using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonAttendanceToTopicMastery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttendedLessonCount",
                schema: "progress_tracking",
                table: "topic_masteries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalLessonMinutes",
                schema: "progress_tracking",
                table: "topic_masteries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendedLessonCount",
                schema: "progress_tracking",
                table: "topic_masteries");

            migrationBuilder.DropColumn(
                name: "TotalLessonMinutes",
                schema: "progress_tracking",
                table: "topic_masteries");
        }
    }
}
