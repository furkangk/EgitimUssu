using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Teachers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TeacherCancellationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CancellationChargePercent",
                schema: "teachers",
                table: "teacher_profiles",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CancellationNoticeHours",
                schema: "teachers",
                table: "teacher_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationChargePercent",
                schema: "teachers",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "CancellationNoticeHours",
                schema: "teachers",
                table: "teacher_profiles");
        }
    }
}
