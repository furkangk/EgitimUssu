using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Students.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherStudentInviteCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                schema: "students",
                table: "teacher_student_links",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_links_InviteCode",
                schema: "students",
                table: "teacher_student_links",
                column: "InviteCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_teacher_student_links_InviteCode",
                schema: "students",
                table: "teacher_student_links");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                schema: "students",
                table: "teacher_student_links");
        }
    }
}
