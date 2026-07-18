using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Scheduling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonRescheduleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalStartAtUtc",
                schema: "scheduling",
                table: "lesson_schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RescheduleNote",
                schema: "scheduling",
                table: "lesson_schedules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalStartAtUtc",
                schema: "scheduling",
                table: "lesson_schedules");

            migrationBuilder.DropColumn(
                name: "RescheduleNote",
                schema: "scheduling",
                table: "lesson_schedules");
        }
    }
}
