using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Scheduling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyLessonSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ç-06: Öğrencinin kendi dersi (StudyScheduleEntry) birleşik lesson_schedules'e taşınır
            // (TeacherUserId null = self). Önce kolonlar hazırlanır, sonra veri taşınır, EN SON tablo düşürülür.

            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherUserId",
                schema: "scheduling",
                table: "lesson_schedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "LessonFormat",
                schema: "scheduling",
                table: "lesson_schedules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                schema: "scheduling",
                table: "lesson_schedules",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                schema: "scheduling",
                table: "lesson_schedules",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_schedules_StudentId_TeacherUserId_StartAtUtc",
                schema: "scheduling",
                table: "lesson_schedules",
                columns: new[] { "StudentId", "TeacherUserId", "StartAtUtc" });

            // Veri göçü: study_schedule_entries → lesson_schedules (self ders). Status: Active→Planned, aksi Cancelled.
            migrationBuilder.Sql(@"
INSERT INTO scheduling.lesson_schedules
  (""Id"",""TeacherUserId"",""StudentId"",""Subject"",""Topic"",""LessonFormat"",
   ""StartAtUtc"",""EndAtUtc"",""TimeZone"",""RecurrenceRule"",""Status"",
   ""ReminderOffsetMinutes"",""ColorHex"",""Notes"",""IsChargeable"",
   ""CreatedOnUtc"",""UpdatedOnUtc"")
SELECT ""Id"", NULL, ""StudentId"", ""Subject"", ""Topic"", NULL,
       ""StartAtUtc"", ""EndAtUtc"", ""TimeZone"", ""RecurrenceRule"",
       CASE WHEN ""Status""='Active' THEN 'Planned' ELSE 'Cancelled' END,
       ""ReminderOffsetMinutes"", ""ColorHex"", ""Notes"", false,
       ""CreatedOnUtc"", ""UpdatedOnUtc""
FROM scheduling.study_schedule_entries;");

            // Veri taşındıktan sonra eski tablo düşürülür.
            migrationBuilder.DropTable(
                name: "study_schedule_entries",
                schema: "scheduling");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alma: tabloyu yeniden kur, self dersleri geri taşı, sonra lesson_schedules'ten sil,
            // ancak bundan SONRA kolonları eski (non-null) haline döndür — yoksa null TeacherUserId çakışır.
            migrationBuilder.CreateTable(
                name: "study_schedule_entries",
                schema: "scheduling",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecurrenceRule = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReminderOffsetMinutes = table.Column<int>(type: "integer", nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_schedule_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_schedule_entries_StudentId_Status",
                schema: "scheduling",
                table: "study_schedule_entries",
                columns: new[] { "StudentId", "Status" });

            // Self dersleri geri taşı (Planned→Active, aksi Cancelled), sonra lesson_schedules'ten çıkar.
            migrationBuilder.Sql(@"
INSERT INTO scheduling.study_schedule_entries
  (""Id"",""StudentId"",""Subject"",""Topic"",""StartAtUtc"",""EndAtUtc"",""TimeZone"",
   ""RecurrenceRule"",""ReminderOffsetMinutes"",""ColorHex"",""Notes"",""Status"",
   ""CreatedOnUtc"",""UpdatedOnUtc"")
SELECT ""Id"",""StudentId"",""Subject"",""Topic"",""StartAtUtc"",""EndAtUtc"",""TimeZone"",
       ""RecurrenceRule"",""ReminderOffsetMinutes"",""ColorHex"",""Notes"",
       CASE WHEN ""Status""='Cancelled' THEN 'Cancelled' ELSE 'Active' END,
       ""CreatedOnUtc"",""UpdatedOnUtc""
FROM scheduling.lesson_schedules WHERE ""TeacherUserId"" IS NULL;");

            migrationBuilder.Sql(@"DELETE FROM scheduling.lesson_schedules WHERE ""TeacherUserId"" IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_lesson_schedules_StudentId_TeacherUserId_StartAtUtc",
                schema: "scheduling",
                table: "lesson_schedules");

            migrationBuilder.DropColumn(
                name: "ColorHex",
                schema: "scheduling",
                table: "lesson_schedules");

            migrationBuilder.DropColumn(
                name: "Topic",
                schema: "scheduling",
                table: "lesson_schedules");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherUserId",
                schema: "scheduling",
                table: "lesson_schedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LessonFormat",
                schema: "scheduling",
                table: "lesson_schedules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
