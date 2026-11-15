using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Assignments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentDueScanIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_assignments_due_soon_scan",
                schema: "assignments",
                table: "assignments",
                columns: new[] { "DueDateUtc", "Id" },
                filter: "\"SubmittedOnUtc\" IS NULL AND \"Status\" IN ('Pending', 'ReturnedForRevision') AND \"DueSoonNotifiedOnUtc\" IS NULL AND \"DueDateUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_missed_scan",
                schema: "assignments",
                table: "assignments",
                columns: new[] { "DueDateUtc", "Id" },
                filter: "\"SubmittedOnUtc\" IS NULL AND \"Status\" IN ('Pending', 'ReturnedForRevision') AND \"MissedNotifiedOnUtc\" IS NULL AND \"DueDateUtc\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_assignments_due_soon_scan",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "IX_assignments_missed_scan",
                schema: "assignments",
                table: "assignments");
        }
    }
}
