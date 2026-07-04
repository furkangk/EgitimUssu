using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Parents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "parents");

            migrationBuilder.CreateTable(
                name: "child_progress_snapshots",
                schema: "parents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedLessonCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedLessonCount = table.Column<int>(type: "integer", nullable: false),
                    LastLessonCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalAssignmentCount = table.Column<int>(type: "integer", nullable: false),
                    OpenAssignmentCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAssignmentCount = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ExpectedPaymentTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CollectedPaymentTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OutstandingPaymentTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LastPaymentUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WeeklyStudyMinutes = table.Column<int>(type: "integer", nullable: false),
                    StudyStreakDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_child_progress_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "known_students",
                schema: "parents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_known_students", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "module_states",
                schema: "parents",
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
                schema: "parents",
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
                name: "parent_child_links",
                schema: "parents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Relationship = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InviteCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsPrimaryContact = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_child_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parent_profiles",
                schema: "parents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NotifyMissedAssignment = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyWeeklyProgressSummary = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyLessonReminders = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyTestResults = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyPayments = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationChannel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parent_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "processed_integration_events",
                schema: "parents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_integration_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_child_progress_snapshots_StudentId",
                schema: "parents",
                table: "child_progress_snapshots",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_known_students_StudentId",
                schema: "parents",
                table: "known_students",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc",
                schema: "parents",
                table: "outbox_messages",
                column: "ProcessedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_parent_child_links_ParentUserId_StudentId",
                schema: "parents",
                table: "parent_child_links",
                columns: new[] { "ParentUserId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_parent_child_links_StudentId",
                schema: "parents",
                table: "parent_child_links",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_parent_profiles_UserId",
                schema: "parents",
                table: "parent_profiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "child_progress_snapshots",
                schema: "parents");

            migrationBuilder.DropTable(
                name: "known_students",
                schema: "parents");

            migrationBuilder.DropTable(
                name: "module_states",
                schema: "parents");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "parents");

            migrationBuilder.DropTable(
                name: "parent_child_links",
                schema: "parents");

            migrationBuilder.DropTable(
                name: "parent_profiles",
                schema: "parents");

            migrationBuilder.DropTable(
                name: "processed_integration_events",
                schema: "parents");
        }
    }
}
