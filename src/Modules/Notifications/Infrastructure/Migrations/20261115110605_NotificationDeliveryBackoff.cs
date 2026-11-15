using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificationDeliveryBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresOnUtc",
                schema: "notifications",
                table: "user_notifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptOnUtc",
                schema: "notifications",
                table: "user_notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PushAttempts",
                schema: "notifications",
                table: "user_notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PushStatus",
                schema: "notifications",
                table: "user_notifications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            // Eski teslim durumunu koru; TTL kaynak oluşturma anından türetilir.
            // MessageReceived önizlemesi silinmiş bir mesajı bile sızdırabiliyordu.
            migrationBuilder.Sql("""
                UPDATE notifications.user_notifications SET
                    "ExpiresOnUtc" = "CreatedOnUtc" + CASE WHEN "Kind" = 'LessonReminder'
                        THEN interval '30 minutes' ELSE interval '24 hours' END,
                    "PushStatus" = CASE WHEN "PushedOnUtc" IS NULL THEN 'Pending' ELSE 'Sent' END;
                UPDATE notifications.user_notifications
                    SET "Body" = 'Bir kullanıcı size mesaj gönderdi.' WHERE "Kind" = 'MessageReceived';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_user_notifications_push_due",
                schema: "notifications",
                table: "user_notifications",
                columns: new[] { "NextAttemptOnUtc", "CreatedOnUtc", "Id" },
                filter: "\"PushStatus\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Güvenli metin korunur; eski özel içerik geri oluşturulamaz ve oluşturulmamalıdır.
            migrationBuilder.DropIndex(
                name: "IX_user_notifications_push_due",
                schema: "notifications",
                table: "user_notifications");

            migrationBuilder.DropColumn(
                name: "ExpiresOnUtc",
                schema: "notifications",
                table: "user_notifications");

            migrationBuilder.DropColumn(
                name: "NextAttemptOnUtc",
                schema: "notifications",
                table: "user_notifications");

            migrationBuilder.DropColumn(
                name: "PushAttempts",
                schema: "notifications",
                table: "user_notifications");

            migrationBuilder.DropColumn(
                name: "PushStatus",
                schema: "notifications",
                table: "user_notifications");
        }
    }
}
