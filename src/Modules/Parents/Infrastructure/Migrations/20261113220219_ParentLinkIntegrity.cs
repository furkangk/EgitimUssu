using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Parents.Infrastructure.Migrations
{
    /// <summary>
    /// P15 / N-32: Öğrenci başına tek onaylı birincil veli — kısmi benzersiz indeks.
    /// <para>
    /// Master §1 "Veri göçü": indeks kurulmadan önce mevcut tekrarlar temizlenir. Aynı öğrencinin birden çok onaylı
    /// birincil velisi varsa en erken bağlanan (<c>LinkedOnUtc</c>, yoksa <c>RequestedOnUtc</c>; eşitlikte kimlik) birincil
    /// kalır, diğerleri ikincile çevrilir — bağ ve erişim korunur, yalnız "birincil" rozeti düşer. <c>Down</c> indeksi
    /// kaldırır; düşürülen rozetler geri getirilmez (hangi satırın tekrar olduğu bilgisi indeksle birlikte anlamsızlaşır).
    /// </para>
    /// </summary>
    public partial class ParentLinkIntegrity : Migration
    {
        /// <summary>Kısmi benzersiz indeksin adı.</summary>
        public const string PrimaryContactIndexName = "IX_parent_child_links_StudentId_primary";

        /// <summary>Fazla onaylı birincil velileri en erken bağlanan hariç ikincile çeviren göç SQL'i.</summary>
        public const string DemoteDuplicatePrimariesSql = """
            UPDATE parents.parent_child_links AS link
            SET "IsPrimaryContact" = FALSE,
                "UpdatedOnUtc" = now()
            FROM (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "StudentId"
                           ORDER BY COALESCE("LinkedOnUtc", "RequestedOnUtc"), "Id") AS rank
                FROM parents.parent_child_links
                WHERE "IsPrimaryContact" AND "Status" = 'Approved'
            ) AS ranked
            WHERE link."Id" = ranked."Id" AND ranked.rank > 1;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DemoteDuplicatePrimariesSql);

            migrationBuilder.CreateIndex(
                name: PrimaryContactIndexName,
                schema: "parents",
                table: "parent_child_links",
                column: "StudentId",
                unique: true,
                filter: "\"IsPrimaryContact\" AND \"Status\" = 'Approved'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: PrimaryContactIndexName,
                schema: "parents",
                table: "parent_child_links");
        }
    }
}
