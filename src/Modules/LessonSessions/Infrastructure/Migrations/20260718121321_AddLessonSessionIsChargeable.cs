using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.LessonSessions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonSessionIsChargeable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsChargeable",
                schema: "lesson_sessions",
                table: "lesson_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsChargeable",
                schema: "lesson_sessions",
                table: "lesson_sessions");
        }
    }
}
