using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_performance_projections",
                schema: "reporting",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    LastAppliedEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_performance_projections", x => new { x.StudentId, x.Subject, x.Year, x.Month });
                });

            migrationBuilder.CreateTable(
                name: "student_study_projections",
                schema: "reporting",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    WeekOfYear = table.Column<int>(type: "integer", nullable: false),
                    LastAppliedEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_study_projections", x => new { x.StudentId, x.Year, x.WeekOfYear });
                });

            migrationBuilder.CreateTable(
                name: "teacher_monthly_projections",
                schema: "reporting",
                columns: table => new
                {
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    CollectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LastAppliedEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_monthly_projections", x => new { x.TeacherUserId, x.Year, x.Month });
                });

            migrationBuilder.CreateTable(
                name: "student_performance_items",
                schema: "reporting",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ItemKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_performance_items", x => new { x.StudentId, x.Subject, x.Year, x.Month, x.Kind, x.ItemKey });
                    table.ForeignKey(
                        name: "FK_student_performance_items_student_performance_projections_S~",
                        columns: x => new { x.StudentId, x.Subject, x.Year, x.Month },
                        principalSchema: "reporting",
                        principalTable: "student_performance_projections",
                        principalColumns: new[] { "StudentId", "Subject", "Year", "Month" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_study_items",
                schema: "reporting",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    WeekOfYear = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_study_items", x => new { x.StudentId, x.Year, x.WeekOfYear, x.Kind, x.ItemId });
                    table.ForeignKey(
                        name: "FK_student_study_items_student_study_projections_StudentId_Yea~",
                        columns: x => new { x.StudentId, x.Year, x.WeekOfYear },
                        principalSchema: "reporting",
                        principalTable: "student_study_projections",
                        principalColumns: new[] { "StudentId", "Year", "WeekOfYear" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_monthly_items",
                schema: "reporting",
                columns: table => new
                {
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_monthly_items", x => new { x.TeacherUserId, x.Year, x.Month, x.Kind, x.ItemId });
                    table.ForeignKey(
                        name: "FK_teacher_monthly_items_teacher_monthly_projections_TeacherUs~",
                        columns: x => new { x.TeacherUserId, x.Year, x.Month },
                        principalSchema: "reporting",
                        principalTable: "teacher_monthly_projections",
                        principalColumns: new[] { "TeacherUserId", "Year", "Month" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_performance_items",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "student_study_items",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "teacher_monthly_items",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "student_performance_projections",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "student_study_projections",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "teacher_monthly_projections",
                schema: "reporting");
        }
    }
}
