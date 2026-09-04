using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "progress_snapshots",
                schema: "progress_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    MasteryScore = table.Column<int>(type: "integer", nullable: false),
                    StudyMinutes = table.Column<int>(type: "integer", nullable: false),
                    TestCount = table.Column<int>(type: "integer", nullable: false),
                    AverageNet = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_progress_snapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_progress_snapshots_StudentId_Period_PeriodStart",
                schema: "progress_tracking",
                table: "progress_snapshots",
                columns: new[] { "StudentId", "Period", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_progress_snapshots_StudentId_Subject_Period_PeriodStart",
                schema: "progress_tracking",
                table: "progress_snapshots",
                columns: new[] { "StudentId", "Subject", "Period", "PeriodStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "progress_snapshots",
                schema: "progress_tracking");
        }
    }
}
