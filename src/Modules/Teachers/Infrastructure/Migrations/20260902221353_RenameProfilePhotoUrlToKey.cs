using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Teachers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameProfilePhotoUrlToKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProfilePhotoUrl",
                schema: "teachers",
                table: "teacher_profiles",
                newName: "ProfilePhotoKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProfilePhotoKey",
                schema: "teachers",
                table: "teacher_profiles",
                newName: "ProfilePhotoUrl");
        }
    }
}
