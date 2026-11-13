namespace EgitimUssu.Shared.Contracts;

// M14-4: Assignments uygular; Reporting (PDF rapor özet kutusu) tüketir. Ödev tamamlama oranı
// Reporting'in projeksiyonlarında yoktur ve olay beslemesiyle üretilmesi ayrı bir read-model
// gerektirirdi; rapor anında tek seferlik okunur (K-04: Shared/Contracts okuma arayüzü).

/// <summary>
/// Bir dönemdeki ödev sayıları. <paramref name="TotalCount"/> dönemde <b>teslim tarihi</b> düşen
/// ödevlerin tamamı, <paramref name="CompletedCount"/> ise tamamlanmış/onaylanmış olanlar.
/// </summary>
/// <param name="TotalCount">Dönemdeki toplam ödev sayısı.</param>
/// <param name="CompletedCount">Tamamlanan ödev sayısı.</param>
public sealed record AssignmentCompletionSummary(int TotalCount, int CompletedCount);

public interface IStudentAssignmentDigestDirectory
{
    /// <summary>
    /// Verilen aralıkta öğrencinin ödev tamamlama sayıları. <b>Öğrencinin kullanıcı kimliği</b> beklenir
    /// (Assignments öğrenciyi kullanıcı kimliğiyle tutar, öğrenci profili kimliğiyle değil).
    /// Hiç ödev yoksa (0, 0) döner.
    /// </summary>
    /// <param name="studentUserId">Öğrencinin login kullanıcı kimliği.</param>
    /// <param name="fromUtc">Aralık başlangıcı (dahil).</param>
    /// <param name="toUtc">Aralık bitişi (hariç).</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Toplam ve tamamlanan ödev sayısı.</returns>
    Task<AssignmentCompletionSummary> GetCompletionAsync(
        Guid studentUserId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
