using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Students.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenInviteCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_teacher_student_links_InviteCode",
                schema: "students",
                table: "teacher_student_links");

            migrationBuilder.DropIndex(
                name: "IX_student_parent_invites_InviteCode",
                schema: "students",
                table: "student_parent_invites");

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteExpiresOnUtc",
                schema: "students",
                table: "teacher_student_links",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresOnUtc",
                schema: "students",
                table: "student_parent_invites",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // P15 / N-20 veri göçü (master §1 "Veri göçü" kuralı: benzersiz indeksten ÖNCE geri doldur + tekrarları temizle).
            // 1) Mevcut bekleyen davetler göç anından itibaren 7 gün geçerli; kullanılmış veli davetlerine
            //    oluşturulma + 7 gün yazılır (bilgi amaçlı — kullanılmış davet zaten çözülmez).
            migrationBuilder.Sql("""
                UPDATE students.teacher_student_links
                SET "InviteExpiresOnUtc" = now() + interval '7 days'
                WHERE "Status" = 'InviteSent';

                UPDATE students.student_parent_invites
                SET "ExpiresOnUtc" = CASE
                    WHEN "Status" = 'Pending' THEN now() + interval '7 days'
                    ELSE "CreatedOnUtc" + interval '7 days'
                END;
                """);

            // 2) Eski 6 haneli kodlar çakışabiliyordu. Aynı kodu taşıyan bekleyen davetlerden yalnız en
            //    yenisi kalır: öğretmen–öğrenci bağında diğerleri davetsiz (Manual) duruma çekilir — öğretmen
            //    yeniden davet edebilir; kullanılmamış eski veli davetleri silinir (bağ yok, yalnız kod).
            migrationBuilder.Sql("""
                UPDATE students.teacher_student_links AS l
                SET "Status" = 'Manual', "InviteCode" = NULL, "InviteTargetUserId" = NULL, "InviteExpiresOnUtc" = NULL
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "InviteCode" ORDER BY "UpdatedOnUtc" DESC, "Id") AS rn
                    FROM students.teacher_student_links
                    WHERE "Status" = 'InviteSent' AND "InviteCode" IS NOT NULL
                ) AS d
                WHERE l."Id" = d."Id" AND d.rn > 1;

                DELETE FROM students.student_parent_invites AS i
                USING (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "InviteCode" ORDER BY "CreatedOnUtc" DESC, "Id") AS rn
                    FROM students.student_parent_invites
                    WHERE "Status" = 'Pending'
                ) AS d
                WHERE i."Id" = d."Id" AND d.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_links_InviteCode_pending",
                schema: "students",
                table: "teacher_student_links",
                column: "InviteCode",
                unique: true,
                filter: "\"Status\" = 'InviteSent'");

            migrationBuilder.CreateIndex(
                name: "IX_student_parent_invites_InviteCode_pending",
                schema: "students",
                table: "student_parent_invites",
                column: "InviteCode",
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not: Up'ta silinen yinelenmiş (kullanılmamış) veli davetleri ve davetsiz duruma çekilen
            // bağlar geri getirilmez — yalnız şema geri alınır. Kodlar yine 6 haneli üretilmeye dönmez;
            // o kod değişikliğidir.
            migrationBuilder.DropIndex(
                name: "IX_teacher_student_links_InviteCode_pending",
                schema: "students",
                table: "teacher_student_links");

            migrationBuilder.DropIndex(
                name: "IX_student_parent_invites_InviteCode_pending",
                schema: "students",
                table: "student_parent_invites");

            migrationBuilder.DropColumn(
                name: "InviteExpiresOnUtc",
                schema: "students",
                table: "teacher_student_links");

            migrationBuilder.DropColumn(
                name: "ExpiresOnUtc",
                schema: "students",
                table: "student_parent_invites");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_student_links_InviteCode",
                schema: "students",
                table: "teacher_student_links",
                column: "InviteCode");

            migrationBuilder.CreateIndex(
                name: "IX_student_parent_invites_InviteCode",
                schema: "students",
                table: "student_parent_invites",
                column: "InviteCode");
        }
    }
}
