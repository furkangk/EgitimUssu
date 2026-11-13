namespace EgitimUssu.Shared.Infrastructure.Configuration;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool DispatchEnabled { get; set; }

    public int BatchSize { get; set; } = 20;

    public int PollIntervalSeconds { get; set; } = 15;

    // K5: Dayanıklı işleme parametreleri.
    /// <summary>Bir mesaj dead-letter'a taşınmadan önceki azami deneme sayısı.</summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Üstel backoff taban süresi (sn); gecikme ≈ Base × 2^(RetryCount-1), MaxBackoff ile sınırlı.</summary>
    public int RetryBackoffBaseSeconds { get; set; } = 30;

    /// <summary>Backoff üst sınırı (sn).</summary>
    public int MaxBackoffSeconds { get; set; } = 3600;

    /// <summary>Çoklu-instance'ta claim edilen mesajın görünürlük kilidi (sn); bu süre içinde başka instance almaz.</summary>
    public int ClaimLeaseSeconds { get; set; } = 300;

    /// <summary>
    /// A-09: İşlenmiş outbox/inbox satırlarının saklanma süresi (gün). Tablolar tek yönde
    /// büyüyordu: <c>ProcessedOnUtc</c> işaretleniyor ama satır hiç silinmiyordu — 15 modülün
    /// her birinde. Süre, bir olayı geriye dönük incelemeye yetecek kadar uzun, indeks ve tablo
    /// boyutunu sınırlayacak kadar kısa seçildi. <c>0</c> → budama kapalı.
    /// </summary>
    public int RetentionDays { get; set; } = 14;

    /// <summary>Budama taramasının sıklığı (dk). Poll döngüsünden ayrıdır; her turda silmek gereksizdir.</summary>
    public int PurgeIntervalMinutes { get; set; } = 60;
}
