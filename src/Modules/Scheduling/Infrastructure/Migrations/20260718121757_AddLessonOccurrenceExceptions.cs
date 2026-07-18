using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Scheduling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonOccurrenceExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lesson_occurrence_exceptions",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesLessonScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OverrideStartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverrideEndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_occurrence_exceptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_occurrence_exceptions_SeriesLessonScheduleId_Origina~",
                schema: "scheduling",
                table: "lesson_occurrence_exceptions",
                columns: new[] { "SeriesLessonScheduleId", "OriginalStartAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_occurrence_exceptions",
                schema: "scheduling");
        }
    }
}
