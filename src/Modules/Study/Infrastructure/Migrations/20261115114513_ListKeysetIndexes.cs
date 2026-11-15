using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Study.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ListKeysetIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_test_results_StudentId_TakenOnUtc_Id",
                schema: "study",
                table: "test_results",
                columns: new[] { "StudentId", "TakenOnUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_study_sessions_StudentId_StartedAtUtc_Id",
                schema: "study",
                table: "study_sessions",
                columns: new[] { "StudentId", "StartedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_study_notes_StudentId_CreatedOnUtc_Id",
                schema: "study",
                table: "study_notes",
                columns: new[] { "StudentId", "CreatedOnUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_test_results_StudentId_TakenOnUtc_Id",
                schema: "study",
                table: "test_results");

            migrationBuilder.DropIndex(
                name: "IX_study_sessions_StudentId_StartedAtUtc_Id",
                schema: "study",
                table: "study_sessions");

            migrationBuilder.DropIndex(
                name: "IX_study_notes_StudentId_CreatedOnUtc_Id",
                schema: "study",
                table: "study_notes");
        }
    }
}
