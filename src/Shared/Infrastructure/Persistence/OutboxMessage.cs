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

    /// <summary>
    /// C-04: Mesajı üreten isteğin W3C <c>traceparent</c> değeri (55 karakter,
    /// <c>00-&lt;32 hex trace&gt;-&lt;16 hex span&gt;-&lt;2 hex bayrak&gt;</c>).
    /// <para>
    /// Outbox asenkron bir hop'tur: üretici işlem çoktan bitmiş olur, tüketici dakikalar sonra
    /// başka bir thread'de koşar. Bağlam mesajla birlikte taşınmazsa zincir orada kopar ve
    /// "HTTP → command → EF → outbox → handler" iki ayrı trace olarak görünür. Dispatch bu değeri
    /// okuyup span'ın ebeveyni yapar; böylece zincir tek trace'te kalır.
    /// </para>
    /// <para>Trace yokken (tohumlama, zamanlanmış tarayıcılar) <c>null</c>'dur — akış etkilenmez.</para>
    /// </summary>
    public string? TraceParent { get; set; }
}
