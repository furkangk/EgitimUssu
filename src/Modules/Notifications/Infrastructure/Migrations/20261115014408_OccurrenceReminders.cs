using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OccurrenceReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lesson_reminders_LessonScheduleId",
                schema: "notifications",
                table: "lesson_reminders");

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceStartUtc",
                schema: "notifications",
                table: "lesson_reminders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientUserId",
                schema: "notifications",
                table: "lesson_reminders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Eski şema plan başına bir satır tutuyordu. Modülün kendi kayıtlarından geri doldurulur.
            migrationBuilder.Sql("""
                UPDATE notifications.lesson_reminders
                SET "OccurrenceStartUtc" = "ScheduledLessonStartAtUtc", "RecipientUserId" = "TeacherUserId";
                UPDATE notifications.lesson_reminders SET "Status" = 'Cancelled'
                WHERE "RecipientUserId" = '00000000-0000-0000-0000-000000000000' AND "Status" = 'Pending';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_reminders_LessonScheduleId_OccurrenceStartUtc_Recipi~",
                schema: "notifications",
                table: "lesson_reminders",
                columns: new[] { "LessonScheduleId", "OccurrenceStartUtc", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_reminders_Status_RemindAtUtc",
                schema: "notifications",
                table: "lesson_reminders",
                columns: new[] { "Status", "RemindAtUtc" },
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lesson_reminders_LessonScheduleId_OccurrenceStartUtc_Recipi~",
                schema: "notifications",
                table: "lesson_reminders");

            migrationBuilder.DropIndex(
                name: "IX_lesson_reminders_Status_RemindAtUtc",
                schema: "notifications",
                table: "lesson_reminders");

            // Eski şema yalnız tek satır tutabilir: öğretmenin en yakın tarihli satırı önceliklidir.
            migrationBuilder.Sql("""
                DELETE FROM notifications.lesson_reminders WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", row_number() OVER (PARTITION BY "LessonScheduleId"
                            ORDER BY ("RecipientUserId" = "TeacherUserId") DESC,
                                "OccurrenceStartUtc" DESC, "CreatedOnUtc", "Id") AS rn
                        FROM notifications.lesson_reminders
                    ) ranked WHERE rn > 1
                );
                """);

            migrationBuilder.DropColumn(
                name: "OccurrenceStartUtc",
                schema: "notifications",
                table: "lesson_reminders");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                schema: "notifications",
                table: "lesson_reminders");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_reminders_LessonScheduleId",
                schema: "notifications",
                table: "lesson_reminders",
                column: "LessonScheduleId",
                unique: true);
        }
    }
}
