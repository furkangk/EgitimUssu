using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionLastUsedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedOnUtc",
                schema: "identity",
                table: "refresh_token_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // M01-4: Var olan oturumların "son kullanım"ı bilinmiyor; en yakın doğru değer
            // açılış anıdır. Varsayılan 0001-01-01 bırakılsaydı ekranda "son kullanım: yıl 1"
            // yazardı — yanlış bir bilgi, boş bir alandan daha kötüdür.
            migrationBuilder.Sql(
                """
                UPDATE identity.refresh_token_sessions
                SET "LastUsedOnUtc" = "CreatedOnUtc"
                WHERE "LastUsedOnUtc" = '0001-01-01 00:00:00+00';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_token_sessions_UserAccountId_RevokedOnUtc",
                schema: "identity",
                table: "refresh_token_sessions",
                columns: new[] { "UserAccountId", "RevokedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_token_sessions_UserAccountId_RevokedOnUtc",
                schema: "identity",
                table: "refresh_token_sessions");

            migrationBuilder.DropColumn(
                name: "LastUsedOnUtc",
                schema: "identity",
                table: "refresh_token_sessions");
        }
    }
}
