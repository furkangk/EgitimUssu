using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Identity.Infrastructure.Migrations
{
    /// <summary>
    /// P15 T7: Oturum iptal nedeni (N-29) + kullanıcı başına tek bekleyen silme talebi (N-32).
    /// <para>
    /// Veri göçü (çıta §1 "Veri göçü"): yeni kolon geriye dönük doldurulur ve benzersiz indeksten
    /// <b>önce</b> tekrarlar temizlenir. SQL'ler <c>public const</c> — gerçek Postgres'te sınanır (#195b).
    /// </para>
    /// </summary>
    public partial class AddSessionRevocationReason : Migration
    {
        /// <summary>Kısmi benzersiz indeksin adı (<c>AccountDeletionRequestConfiguration</c> ile aynı).</summary>
        public const string PendingDeletionIndexName = "IX_account_deletion_requests_UserAccountId_pending";

        /// <summary>
        /// Var olan iptal edilmiş oturumlar <c>Rotated</c> kabul edilir. Nedeni bilinmeyen eski iptalin
        /// token'ı geri gelirse yeniden kullanım tespiti (A-15) eskisi gibi çalışır — güvenli taraf.
        /// </summary>
        public const string BackfillRotatedReasonSql = """
            UPDATE identity.refresh_token_sessions
            SET "RevocationReason" = 'Rotated'
            WHERE "RevokedOnUtc" IS NOT NULL AND "RevocationReason" IS NULL;
            """;

        /// <summary>
        /// Bir kullanıcının fazla bekleyen talepleri iptal edilir; <b>en eski</b> kalır — yenisini tutmak
        /// bekleme süresini sıfırlayıp silmeyi geciktirirdi.
        /// </summary>
        public const string CancelDuplicatePendingDeletionsSql = """
            UPDATE identity.account_deletion_requests AS request
            SET "Status" = 'Cancelled',
                "CancelledOnUtc" = now()
            FROM (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "UserAccountId"
                           ORDER BY "RequestedOnUtc", "Id") AS rank
                FROM identity.account_deletion_requests
                WHERE "Status" = 'Pending'
            ) AS ranked
            WHERE request."Id" = ranked."Id" AND ranked.rank > 1;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                schema: "identity",
                table: "refresh_token_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(BackfillRotatedReasonSql);
            migrationBuilder.Sql(CancelDuplicatePendingDeletionsSql);

            migrationBuilder.CreateIndex(
                name: PendingDeletionIndexName,
                schema: "identity",
                table: "account_deletion_requests",
                column: "UserAccountId",
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // İptal edilen tekrar talepler geri açılmaz: hangisinin gerçekten bekleyen olduğu bilgisi
            // göçte bilerek atıldı (en eski kaldı). İndeks ve kolon geri alınır.
            migrationBuilder.DropIndex(
                name: PendingDeletionIndexName,
                schema: "identity",
                table: "account_deletion_requests");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                schema: "identity",
                table: "refresh_token_sessions");
        }
    }
}
