using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Assignments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ListKeysetIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_assignments_LessonSessionId_CreatedOnUtc_Id",
                schema: "assignments",
                table: "assignments",
                columns: new[] { "LessonSessionId", "CreatedOnUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_StudentId_CreatedOnUtc_Id",
                schema: "assignments",
                table: "assignments",
                columns: new[] { "StudentId", "CreatedOnUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_TeacherUserId_CreatedOnUtc_Id",
                schema: "assignments",
                table: "assignments",
                columns: new[] { "TeacherUserId", "CreatedOnUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_assignments_LessonSessionId_CreatedOnUtc_Id",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "IX_assignments_StudentId_CreatedOnUtc_Id",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "IX_assignments_TeacherUserId_CreatedOnUtc_Id",
                schema: "assignments",
                table: "assignments");
        }
    }
}
