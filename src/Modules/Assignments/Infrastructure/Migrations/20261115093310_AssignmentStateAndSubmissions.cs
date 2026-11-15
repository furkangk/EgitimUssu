using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Assignments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentStateAndSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLate",
                schema: "assignments",
                table: "assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedOnUtc",
                schema: "assignments",
                table: "assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "assignment_submission_versions",
                schema: "assignments",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    SubmittedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsLate = table.Column<bool>(type: "boolean", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    GradedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TeacherFeedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_submission_versions", x => new { x.AssignmentId, x.Number });
                    table.ForeignKey(
                        name: "FK_assignment_submission_versions_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "assignments",
                        principalTable: "assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Eski modelde UpdatedOnUtc yok. Dayanıklı outbox'taki son TESLİM zamanı tercih edilir;
            // tutulmamış/budanmış olaylarda CompletedOnUtc, sonra CreatedOnUtc yaklaşık yedektir.
            // Öğretmenin Pending eki teslim değildir. Zehirli JSON göçü durdurmamalı.
            migrationBuilder.Sql("""
                WITH envelopes AS (
                    SELECT CASE WHEN pg_input_is_valid("Payload", 'jsonb') THEN "Payload"::jsonb END AS envelope
                    FROM assignments.outbox_messages
                    WHERE "Type" IN ('AssignmentSubmittedDomainEvent', 'AssignmentCompletedDomainEvent')
                ), events AS (
                    SELECT CASE WHEN pg_input_is_valid(envelope->>'payload', 'jsonb')
                        THEN (envelope->>'payload')::jsonb END AS body
                    FROM envelopes
                ), times AS (
                    SELECT body->>'assignmentId' AS id,
                        MAX(CASE WHEN pg_input_is_valid(COALESCE(body->>'submittedOnUtc', body->>'completedOnUtc'), 'timestamptz')
                            THEN COALESCE(body->>'submittedOnUtc', body->>'completedOnUtc')::timestamptz END) AS submitted
                    FROM events GROUP BY body->>'assignmentId'
                )
                UPDATE assignments.assignments a
                SET "SubmittedOnUtc" = COALESCE(t.submitted, a."CompletedOnUtc", a."CreatedOnUtc")
                FROM (SELECT a2."Id", times.submitted FROM assignments.assignments a2
                    LEFT JOIN times ON times.id = a2."Id"::text) t
                WHERE a."Id" = t."Id" AND a."Status" IN ('InProgress', 'Completed', 'Approved', 'ReturnedForRevision');

                UPDATE assignments.assignments
                SET "IsLate" = "SubmittedOnUtc" >
                    ((("DueDateUtc" AT TIME ZONE 'Europe/Istanbul')::date + INTERVAL '1 day') AT TIME ZONE 'Europe/Istanbul') - INTERVAL '1 microsecond'
                WHERE "SubmittedOnUtc" IS NOT NULL AND "DueDateUtc" IS NOT NULL;

                INSERT INTO assignments.assignment_submission_versions
                ("AssignmentId", "Number", "SubmittedOnUtc", "IsLate", "AttachmentUrl", "StorageKey", "FileName", "ContentType", "SizeBytes", "Score", "GradedOnUtc", "TeacherFeedback")
                SELECT "Id", 1, "SubmittedOnUtc", "IsLate", "AttachmentUrl", "AttachmentStorageKey", "AttachmentFileName", "AttachmentContentType", "AttachmentSizeBytes", "Score", "GradedOnUtc", "TeacherFeedback"
                FROM assignments.assignments WHERE "SubmittedOnUtc" IS NOT NULL;

                UPDATE assignments.assignments SET "Score" = NULL, "GradedOnUtc" = NULL
                WHERE "Status" = 'ReturnedForRevision';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignment_submission_versions",
                schema: "assignments");

            migrationBuilder.DropColumn(
                name: "IsLate",
                schema: "assignments",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "SubmittedOnUtc",
                schema: "assignments",
                table: "assignments");
        }
    }
}
