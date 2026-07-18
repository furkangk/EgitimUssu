using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Teachers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherSubjectsAndCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teacher_certificates",
                schema: "teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Institution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_certificates_teacher_profiles_TeacherProfileId",
                        column: x => x.TeacherProfileId,
                        principalSchema: "teachers",
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_subjects",
                schema: "teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_subjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_subjects_teacher_profiles_TeacherProfileId",
                        column: x => x.TeacherProfileId,
                        principalSchema: "teachers",
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_certificates_TeacherProfileId",
                schema: "teachers",
                table: "teacher_certificates",
                column: "TeacherProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_subjects_TeacherProfileId",
                schema: "teachers",
                table: "teacher_subjects",
                column: "TeacherProfileId");

            // Mevcut profillerin birincil branşını çoklu branş tablosuna taşı (geriye uyum).
            migrationBuilder.Sql(@"
                INSERT INTO teachers.teacher_subjects (""Id"", ""TeacherProfileId"", ""Subject"")
                SELECT gen_random_uuid(), tp.""Id"", tp.""Subject""
                FROM teachers.teacher_profiles tp
                WHERE tp.""Subject"" IS NOT NULL AND tp.""Subject"" <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teacher_certificates",
                schema: "teachers");

            migrationBuilder.DropTable(
                name: "teacher_subjects",
                schema: "teachers");
        }
    }
}
