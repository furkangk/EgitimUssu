using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProgressTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "progress_tracking");

            migrationBuilder.CreateTable(
                name: "module_states",
                schema: "progress_tracking",
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
                schema: "progress_tracking",
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
                name: "processed_events",
                schema: "progress_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "topic_goals",
                schema: "progress_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TargetMasteryLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetNetRatio = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    SetByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetByRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AchievedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_goals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "topic_masteries",
                schema: "progress_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MasteryLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MasteryScore = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    TotalStudyMinutes = table.Column<int>(type: "integer", nullable: false),
                    TestAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    AverageNetRatio = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    NetRatioSum = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    RecentNetRatio = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    PriorNetRatio = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: true),
                    Trend = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsWeakSpot = table.Column<bool>(type: "boolean", nullable: false),
                    IsStrength = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastEvaluatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_masteries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedOnUtc",
                schema: "progress_tracking",
                table: "outbox_messages",
                column: "ProcessedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_topic_goals_StudentId_Status",
                schema: "progress_tracking",
                table: "topic_goals",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_topic_masteries_StudentId_IsWeakSpot",
                schema: "progress_tracking",
                table: "topic_masteries",
                columns: new[] { "StudentId", "IsWeakSpot" });

            migrationBuilder.CreateIndex(
                name: "IX_topic_masteries_StudentId_Subject_Topic",
                schema: "progress_tracking",
                table: "topic_masteries",
                columns: new[] { "StudentId", "Subject", "Topic" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_states",
                schema: "progress_tracking");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "progress_tracking");

            migrationBuilder.DropTable(
                name: "processed_events",
                schema: "progress_tracking");

            migrationBuilder.DropTable(
                name: "topic_goals",
                schema: "progress_tracking");

            migrationBuilder.DropTable(
                name: "topic_masteries",
                schema: "progress_tracking");
        }
    }
}
