using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Study.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStreakThresholdPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StreakThresholdPercent",
                schema: "study",
                table: "study_goals",
                type: "integer",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StreakThresholdPercent",
                schema: "study",
                table: "study_goals");
        }
    }
}
