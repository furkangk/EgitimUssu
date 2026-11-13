using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Assignments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentGradeAndDueTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueSoonNotifiedOnUtc",
                schema: "assignments",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradedOnUtc",
                schema: "assignments",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MissedNotifiedOnUtc",
                schema: "assignments",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                schema: "assignments",
                table: "assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_assignments_Status_DueDateUtc",
                schema: "assignments",
                table: "assignments",
                columns: new[] { "Status", "DueDateUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_assignments_Status_DueDateUtc",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "DueSoonNotifiedOnUtc",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "GradedOnUtc",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "MissedNotifiedOnUtc",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "assignments",
                table: "assignments");
        }
    }
}
