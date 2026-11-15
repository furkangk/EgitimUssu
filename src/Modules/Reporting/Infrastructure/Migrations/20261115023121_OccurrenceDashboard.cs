using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Reporting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OccurrenceDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_teacher_dashboard_items",
                schema: "reporting",
                table: "teacher_dashboard_items");

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurrenceStartUtc",
                schema: "reporting",
                table: "teacher_dashboard_items",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                schema: "reporting",
                table: "teacher_dashboard_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Modüller arası tablo okunmaz; eski tekil read-model satırının kendi saati kullanılır.
            migrationBuilder.Sql("""
                UPDATE reporting.teacher_dashboard_items
                SET "OccurrenceStartUtc" = COALESCE("OccursAtUtc", TIMESTAMPTZ '1970-01-01 00:00:00+00')
                WHERE "Kind" = 1;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_teacher_dashboard_items",
                schema: "reporting",
                table: "teacher_dashboard_items",
                columns: new[] { "TeacherUserId", "Kind", "ItemId", "OccurrenceStartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski şema seri başına bir satır tutar; bu türetilebilir ders verisidir.
            // Diğer item türleri ve projeksiyonlar korunur.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT ctid, ROW_NUMBER() OVER (
                        PARTITION BY "TeacherUserId", "Kind", "ItemId"
                        ORDER BY "IsCancelled", "OccursAtUtc" DESC NULLS LAST, "OccurrenceStartUtc" DESC
                    ) AS rn
                    FROM reporting.teacher_dashboard_items WHERE "Kind" = 1
                )
                DELETE FROM reporting.teacher_dashboard_items WHERE ctid IN (SELECT ctid FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_teacher_dashboard_items",
                schema: "reporting",
                table: "teacher_dashboard_items");

            migrationBuilder.DropColumn(
                name: "OccurrenceStartUtc",
                schema: "reporting",
                table: "teacher_dashboard_items");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                schema: "reporting",
                table: "teacher_dashboard_items");

            migrationBuilder.AddPrimaryKey(
                name: "PK_teacher_dashboard_items",
                schema: "reporting",
                table: "teacher_dashboard_items",
                columns: new[] { "TeacherUserId", "Kind", "ItemId" });
        }
    }
}
