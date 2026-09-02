using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Assignments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentAttachmentStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                schema: "assignments",
                table: "assignments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                schema: "assignments",
                table: "assignments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttachmentSizeBytes",
                schema: "assignments",
                table: "assignments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentStorageKey",
                schema: "assignments",
                table: "assignments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "AttachmentSizeBytes",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "AttachmentStorageKey",
                schema: "assignments",
                table: "assignments");
        }
    }
}
