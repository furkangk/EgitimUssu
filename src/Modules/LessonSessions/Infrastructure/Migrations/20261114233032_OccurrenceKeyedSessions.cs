using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.LessonSessions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OccurrenceKeyedSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lesson_sessions_LessonScheduleId",
                schema: "lesson_sessions",
                table: "lesson_sessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceStartUtc",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                type: "timestamp with time zone",
                nullable: true);

            // S-3: önce geri doldur, sonra en erken kayıt dışındaki tekrarları iptal et, en son indeks.
            migrationBuilder.Sql("""
                UPDATE lesson_sessions.lesson_sessions
                SET "OccurrenceStartUtc" = "PlannedStartAtUtc"
                WHERE "LessonScheduleId" IS NOT NULL;

                DO $$
                DECLARE duplicate_count integer;
                BEGIN
                    WITH ranked AS (
                        SELECT "Id", row_number() OVER (
                            PARTITION BY "LessonScheduleId", "OccurrenceStartUtc"
                            ORDER BY "CreatedOnUtc", "Id") AS position
                        FROM lesson_sessions.lesson_sessions
                        WHERE "LessonScheduleId" IS NOT NULL
                    )
                    UPDATE lesson_sessions.lesson_sessions AS target
                    SET "Status" = 'Cancelled', "OccurrenceStartUtc" = NULL
                    FROM ranked
                    WHERE target."Id" = ranked."Id" AND ranked.position > 1;
                    GET DIAGNOSTICS duplicate_count = ROW_COUNT;
                    RAISE NOTICE 'OccurrenceKeyedSessions: % duplicate session(s) cancelled', duplicate_count;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_sessions_LessonScheduleId_OccurrenceStartUtc",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                columns: new[] { "LessonScheduleId", "OccurrenceStartUtc" },
                unique: true,
                filter: "\"LessonScheduleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lesson_sessions_LessonScheduleId_OccurrenceStartUtc",
                schema: "lesson_sessions",
                table: "lesson_sessions");

            migrationBuilder.DropColumn(
                name: "OccurrenceStartUtc",
                schema: "lesson_sessions",
                table: "lesson_sessions");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_sessions_LessonScheduleId",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                column: "LessonScheduleId");
        }
    }
}
