using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Payments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CancellationOccurrenceCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LessonScheduleId",
                schema: "payments",
                table: "payment_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceStartUtc",
                schema: "payments",
                table: "payment_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_LessonScheduleId_OccurrenceStartUtc",
                schema: "payments",
                table: "payment_records",
                columns: new[] { "LessonScheduleId", "OccurrenceStartUtc" },
                unique: true,
                filter: "\"LessonScheduleId\" IS NOT NULL AND \"ItemType\" = 'LateCancellation'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_records_LessonScheduleId_OccurrenceStartUtc",
                schema: "payments",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "LessonScheduleId",
                schema: "payments",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "OccurrenceStartUtc",
                schema: "payments",
                table: "payment_records");
        }
    }
}
