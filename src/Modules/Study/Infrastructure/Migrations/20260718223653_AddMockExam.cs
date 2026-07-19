using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Study.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMockExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MockExamId",
                schema: "study",
                table: "test_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mock_exams",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TakenOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalNet = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    EstimatedRank = table.Column<int>(type: "integer", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_exams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_test_results_MockExamId",
                schema: "study",
                table: "test_results",
                column: "MockExamId");

            migrationBuilder.CreateIndex(
                name: "IX_mock_exams_StudentId_TakenOnUtc",
                schema: "study",
                table: "mock_exams",
                columns: new[] { "StudentId", "TakenOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mock_exams",
                schema: "study");

            migrationBuilder.DropIndex(
                name: "IX_test_results_MockExamId",
                schema: "study",
                table: "test_results");

            migrationBuilder.DropColumn(
                name: "MockExamId",
                schema: "study",
                table: "test_results");
        }
    }
}
