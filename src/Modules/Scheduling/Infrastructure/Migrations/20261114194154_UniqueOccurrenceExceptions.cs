using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimUssu.Modules.Scheduling.Infrastructure.Migrations
{
    /// <summary>
    /// P16 T3 / N-05: bir oluşuma tek istisna. Benzersiz indeksten önce aynı <c>(SeriesLessonScheduleId,
    /// OriginalStartAtUtc)</c> anahtarlı tekrarlar silinir; en son <c>CreatedOnUtc</c> (eşitlikte en büyük <c>Id</c>)
    /// kalır — eski kodda son yazılan satır kullanıcının son kararıydı. <c>Down</c> indeksi normal indekse çevirir
    /// (silinen tekrarlar geri gelmez; anlamsal olarak zaten ölüydüler). Kanıt: <c>RealOccurrenceExceptionTests</c>.
    /// </summary>
    public partial class UniqueOccurrenceExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM scheduling.lesson_occurrence_exceptions AS target
                USING (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "SeriesLessonScheduleId", "OriginalStartAtUtc"
                               ORDER BY "CreatedOnUtc" DESC, "Id" DESC) AS row_number
                    FROM scheduling.lesson_occurrence_exceptions
                ) AS ranked
                WHERE target."Id" = ranked."Id"
                  AND ranked.row_number > 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_lesson_occurrence_exceptions_SeriesLessonScheduleId_Origina~",
                schema: "scheduling",
                table: "lesson_occurrence_exceptions");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_occurrence_exceptions_SeriesLessonScheduleId_Origina~",
                schema: "scheduling",
                table: "lesson_occurrence_exceptions",
                columns: new[] { "SeriesLessonScheduleId", "OriginalStartAtUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lesson_occurrence_exceptions_SeriesLessonScheduleId_Origina~",
                schema: "scheduling",
                table: "lesson_occurrence_exceptions");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_occurrence_exceptions_SeriesLessonScheduleId_Origina~",
                schema: "scheduling",
                table: "lesson_occurrence_exceptions",
                columns: new[] { "SeriesLessonScheduleId", "OriginalStartAtUtc" });
        }
    }
}
