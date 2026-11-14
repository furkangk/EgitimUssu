using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Scheduling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeRequestOccurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceStartAtUtc",
                schema: "scheduling",
                table: "lesson_change_requests",
                type: "timestamp with time zone",
                nullable: true);

            // P17 T1 / N-01: tekil derslerde talebin oluşumu dersin kendisidir → geri doldurulur (master §1 veri göçü).
            // Tekrarlı derslerin eski talepleri hangi haftayı kastettiğini taşımıyordu; kurtarılamaz → NULL kalır ve kabulde
            // `scheduling.request_occurrence_unknown` (seri kaydırılmaz).
            migrationBuilder.Sql("""
                UPDATE scheduling.lesson_change_requests AS request
                SET "OccurrenceStartAtUtc" = lesson."StartAtUtc"
                FROM scheduling.lesson_schedules AS lesson
                WHERE request."LessonScheduleId" = lesson."Id"
                  AND request."OccurrenceStartAtUtc" IS NULL
                  AND (lesson."RecurrenceRule" IS NULL OR btrim(lesson."RecurrenceRule") = '');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OccurrenceStartAtUtc",
                schema: "scheduling",
                table: "lesson_change_requests");
        }
    }
}
