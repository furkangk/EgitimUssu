using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Teachers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "teachers");

            migrationBuilder.CreateTable(
                name: "module_states",
                schema: "teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_module_states", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "teacher_profiles",
                schema: "teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    District = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Biography = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Headline = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    LessonFormat = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: false),
                    EducationLevel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HourlyRateAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ProfilePhotoUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "teacher_availability_slots",
                schema: "teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsOnlineAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsInPersonAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_availability_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teacher_availability_slots_teacher_profiles_TeacherProfileId",
                        column: x => x.TeacherProfileId,
                        principalSchema: "teachers",
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc",
                schema: "teachers",
                table: "outbox_messages",
                column: "ProcessedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_availability_slots_TeacherProfileId_DayOfWeek_Start~",
                schema: "teachers",
                table: "teacher_availability_slots",
                columns: new[] { "TeacherProfileId", "DayOfWeek", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_profiles_City_Subject",
                schema: "teachers",
                table: "teacher_profiles",
                columns: new[] { "City", "Subject" });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_profiles_UserId",
                schema: "teachers",
                table: "teacher_profiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_states",
                schema: "teachers");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "teachers");

            migrationBuilder.DropTable(
                name: "teacher_availability_slots",
                schema: "teachers");

            migrationBuilder.DropTable(
                name: "teacher_profiles",
                schema: "teachers");
        }
    }
}
