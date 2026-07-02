namespace EgitimUssu.Shared.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string Module { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime OccurredOnUtc { get; set; }

    public DateTime? ProcessedOnUtc { get; set; }

    public string? Error { get; set; }

    // K5: Mesaj-başına dayanıklı işleme alanları.
    /// <summary>Başarısız yayınlama deneme sayısı.</summary>
    public int RetryCount { get; set; }

    /// <summary>Bir sonraki işleme denemesinin en erken zamanı (backoff / claim lease). Null = hemen uygun.</summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>Maksimum deneme aşıldığında zehirli mesajın dead-letter'a taşındığı an. Set ise kuyruktan çıkarılır.</summary>
    public DateTime? DeadLetteredOnUtc { get; set; }
}
