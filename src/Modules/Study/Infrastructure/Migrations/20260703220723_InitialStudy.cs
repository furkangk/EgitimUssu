using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EgitimUssu.Modules.Study.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialStudy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "study");

            migrationBuilder.CreateTable(
                name: "achievements",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    IconKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_achievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "module_states",
                schema: "study",
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
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "student_achievements",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProgressValue = table.Column<int>(type: "integer", nullable: false),
                    EarnedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_achievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_goals",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyGoalMinutes = table.Column<int>(type: "integer", nullable: false),
                    WeeklyGoalMinutes = table.Column<int>(type: "integer", nullable: true),
                    TargetNet = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    TargetScore = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_sessions",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveMinutes = table.Column<int>(type: "integer", nullable: false),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastResumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPausedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PersonalNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsSharedWithParent = table.Column<bool>(type: "boolean", nullable: false),
                    IsSharedWithTeacher = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_streaks",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStreakDays = table.Column<int>(type: "integer", nullable: false),
                    LongestStreakDays = table.Column<int>(type: "integer", nullable: false),
                    LastStudiedOnDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TotalStudyDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_streaks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_students",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareStudyWithParent = table.Column<bool>(type: "boolean", nullable: false),
                    ShareTestsWithParent = table.Column<bool>(type: "boolean", nullable: false),
                    ShareStudyWithTeacher = table.Column<bool>(type: "boolean", nullable: false),
                    ShareTestsWithTeacher = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_students", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "study_topics",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FirstStudiedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastStudiedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalEffectiveMinutes = table.Column<int>(type: "integer", nullable: false),
                    SessionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_topics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "test_results",
                schema: "study",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    TestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TestType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                    Correct = table.Column<int>(type: "integer", nullable: false),
                    Wrong = table.Column<int>(type: "integer", nullable: false),
                    Blank = table.Column<int>(type: "integer", nullable: false),
                    Net = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    PenaltyDivisor = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    TakenOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsSharedWithParent = table.Column<bool>(type: "boolean", nullable: false),
                    IsSharedWithTeacher = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_results", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "study",
                table: "achievements",
                columns: new[] { "Id", "Category", "Code", "Description", "IconKey", "Threshold", "Title" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), "Consistency", "FIRST_SESSION", "İlk çalışma seansını tamamladın.", "flag", 1, "İlk Adım" },
                    { new Guid("11111111-1111-1111-1111-111111111102"), "Consistency", "SESSIONS_10", "10 çalışma seansı tamamladın.", "event_repeat", 10, "Düzenli Çalışan" },
                    { new Guid("11111111-1111-1111-1111-111111111103"), "Streak", "STREAK_3", "3 gün üst üste çalıştın.", "local_fire_department", 3, "3 Günlük Seri" },
                    { new Guid("11111111-1111-1111-1111-111111111104"), "Streak", "STREAK_7", "7 gün üst üste çalıştın.", "local_fire_department", 7, "7 Günlük Seri" },
                    { new Guid("11111111-1111-1111-1111-111111111105"), "Streak", "STREAK_30", "30 gün üst üste çalıştın.", "whatshot", 30, "30 Günlük Seri" },
                    { new Guid("11111111-1111-1111-1111-111111111106"), "StudyTime", "HOURS_10", "Toplam 10 saat çalıştın.", "schedule", 600, "10 Saat" },
                    { new Guid("11111111-1111-1111-1111-111111111107"), "StudyTime", "HOURS_50", "Toplam 50 saat çalıştın.", "schedule", 3000, "50 Saat" },
                    { new Guid("11111111-1111-1111-1111-111111111108"), "StudyTime", "HOURS_100", "Toplam 100 saat çalıştın.", "military_tech", 6000, "100 Saat" },
                    { new Guid("11111111-1111-1111-1111-111111111109"), "TestPerformance", "FIRST_TEST", "İlk denemeni girdin.", "quiz", 1, "İlk Deneme" },
                    { new Guid("11111111-1111-1111-1111-111111111110"), "TestPerformance", "TESTS_10", "10 deneme girdin.", "fact_check", 10, "Deneme Avcısı" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_achievements_Code",
                schema: "study",
                table: "achievements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc",
                schema: "study",
                table: "outbox_messages",
                column: "ProcessedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_student_achievements_StudentId_AchievementCode",
                schema: "study",
                table: "student_achievements",
                columns: new[] { "StudentId", "AchievementCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_goals_StudentId_IsActive",
                schema: "study",
                table: "study_goals",
                columns: new[] { "StudentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_study_sessions_StudentId_StartedAtUtc",
                schema: "study",
                table: "study_sessions",
                columns: new[] { "StudentId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_study_sessions_StudentId_Status",
                schema: "study",
                table: "study_sessions",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_study_streaks_StudentId",
                schema: "study",
                table: "study_streaks",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_students_UserId",
                schema: "study",
                table: "study_students",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_study_topics_StudentId_Subject_Topic",
                schema: "study",
                table: "study_topics",
                columns: new[] { "StudentId", "Subject", "Topic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_results_StudentId_Subject_TakenOnUtc",
                schema: "study",
                table: "test_results",
                columns: new[] { "StudentId", "Subject", "TakenOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "achievements",
                schema: "study");

            migrationBuilder.DropTable(
                name: "module_states",
                schema: "study");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "study");

            migrationBuilder.DropTable(
                name: "student_achievements",
                schema: "study");

            migrationBuilder.DropTable(
                name: "study_goals",
                schema: "study");

            migrationBuilder.DropTable(
                name: "study_sessions",
                schema: "study");

            migrationBuilder.DropTable(
                name: "study_streaks",
                schema: "study");

            migrationBuilder.DropTable(
                name: "study_students",
                schema: "study");

            migrationBuilder.DropTable(
                name: "study_topics",
                schema: "study");

            migrationBuilder.DropTable(
                name: "test_results",
                schema: "study");
        }
    }
}
