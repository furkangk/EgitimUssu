using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Study.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectTopicCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_subject_catalogs",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_subject_catalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "student_topic_catalogs",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_topic_catalogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_subject_catalogs_StudentId_Name",
                schema: "study",
                table: "student_subject_catalogs",
                columns: new[] { "StudentId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_student_topic_catalogs_StudentId",
                schema: "study",
                table: "student_topic_catalogs",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_student_topic_catalogs_SubjectId_OrderIndex",
                schema: "study",
                table: "student_topic_catalogs",
                columns: new[] { "SubjectId", "OrderIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_subject_catalogs",
                schema: "study");

            migrationBuilder.DropTable(
                name: "student_topic_catalogs",
                schema: "study");
        }
    }
}
