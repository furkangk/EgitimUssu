using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MonthlyCancellationOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_teacher_monthly_items",
                schema: "reporting",
                table: "teacher_monthly_items");

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceStartUtc",
                schema: "reporting",
                table: "teacher_monthly_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddPrimaryKey(
                name: "PK_teacher_monthly_items",
                schema: "reporting",
                table: "teacher_monthly_items",
                columns: new[] { "TeacherUserId", "Year", "Month", "Kind", "ItemId", "OccurrenceStartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski şema seri başına bir iptal tutar. Eski epoch satırı varsa onu,
            // yoksa en son oluşumu koru; ödeme/tamamlama item'ları kayıpsız kalır.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "TeacherUserId", "Year", "Month", "Kind", "ItemId", "OccurrenceStartUtc",
                        row_number() OVER (
                            PARTITION BY "TeacherUserId", "Year", "Month", "Kind", "ItemId"
                            ORDER BY ("OccurrenceStartUtc" = TIMESTAMPTZ '1970-01-01 00:00:00+00') DESC, "OccurrenceStartUtc" DESC
                        ) AS rn
                    FROM reporting.teacher_monthly_items WHERE "Kind" = 2
                )
                DELETE FROM reporting.teacher_monthly_items AS item USING ranked AS r
                WHERE r.rn > 1 AND item."TeacherUserId" = r."TeacherUserId" AND item."Year" = r."Year"
                    AND item."Month" = r."Month" AND item."Kind" = r."Kind" AND item."ItemId" = r."ItemId"
                    AND item."OccurrenceStartUtc" = r."OccurrenceStartUtc";
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_teacher_monthly_items",
                schema: "reporting",
                table: "teacher_monthly_items");

            migrationBuilder.DropColumn(
                name: "OccurrenceStartUtc",
                schema: "reporting",
                table: "teacher_monthly_items");

            migrationBuilder.AddPrimaryKey(
                name: "PK_teacher_monthly_items",
                schema: "reporting",
                table: "teacher_monthly_items",
                columns: new[] { "TeacherUserId", "Year", "Month", "Kind", "ItemId" });
        }
    }
}
