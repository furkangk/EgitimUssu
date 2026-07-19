using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Study.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonIdToStudySession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LessonId",
                schema: "study",
                table: "study_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_sessions_LessonId",
                schema: "study",
                table: "study_sessions",
                column: "LessonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_study_sessions_LessonId",
                schema: "study",
                table: "study_sessions");

            migrationBuilder.DropColumn(
                name: "LessonId",
                schema: "study",
                table: "study_sessions");
        }
    }
}
