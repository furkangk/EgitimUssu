using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Students.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentMembershipTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MembershipTier",
                schema: "students",
                table: "student_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Free");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MembershipTier",
                schema: "students",
                table: "student_profiles");
        }
    }
}
