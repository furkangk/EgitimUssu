using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Students.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherStudentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teacher_student_links",
                schema: "students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreedRateAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    InviteTargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_student_links", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_links_TeacherUserId_StudentId",
                schema: "students",
                table: "teacher_student_links",
                columns: new[] { "TeacherUserId", "StudentId" },
                unique: true);

            // Mevcut manuel öğrenciler için geriye-uyum: her CreatedByTeacherUserId'e Manual link üret.
            migrationBuilder.Sql(@"
                INSERT INTO students.teacher_student_links
                    (""Id"", ""TeacherUserId"", ""StudentId"", ""Status"", ""Currency"", ""IsArchived"", ""CreatedOnUtc"", ""UpdatedOnUtc"")
                SELECT gen_random_uuid(), sp.""CreatedByTeacherUserId"", sp.""Id"", 'Manual', 'TRY', false, now(), now()
                FROM students.student_profiles sp
                WHERE sp.""CreatedByTeacherUserId"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teacher_student_links",
                schema: "students");
        }
    }
}
