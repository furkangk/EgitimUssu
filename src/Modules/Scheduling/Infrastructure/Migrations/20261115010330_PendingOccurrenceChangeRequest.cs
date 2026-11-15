using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Scheduling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PendingOccurrenceChangeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski bekleyen tekrarlar silinmez; en erken talep kalır, diğerleri reddedilir.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "LessonScheduleId", "OccurrenceStartAtUtc"
                        ORDER BY "CreatedOnUtc", "Id") AS rn
                    FROM scheduling.lesson_change_requests
                    WHERE "Status" = 'Pending' AND "OccurrenceStartAtUtc" IS NOT NULL
                )
                UPDATE scheduling.lesson_change_requests AS r
                SET "Status" = 'Rejected', "ResolvedOnUtc" = CURRENT_TIMESTAMP
                FROM ranked WHERE r."Id" = ranked."Id" AND ranked.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_pending_occurrence",
                schema: "scheduling",
                table: "lesson_change_requests",
                columns: new[] { "LessonScheduleId", "OccurrenceStartAtUtc" },
                unique: true,
                filter: "\"Status\" = 'Pending' AND \"OccurrenceStartAtUtc\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_change_requests_pending_occurrence",
                schema: "scheduling",
                table: "lesson_change_requests");
        }
    }
}
