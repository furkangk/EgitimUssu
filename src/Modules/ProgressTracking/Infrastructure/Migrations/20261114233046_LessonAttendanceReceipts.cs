using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LessonAttendanceReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lesson_attendance_receipts",
                schema: "progress_tracking",
                columns: table => new
                {
                    LessonSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_attendance_receipts", x => x.LessonSessionId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_attendance_receipts",
                schema: "progress_tracking");
        }
    }
}
