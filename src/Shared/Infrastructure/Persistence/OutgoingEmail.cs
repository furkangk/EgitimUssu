namespace EgitimUssu.Shared.Infrastructure.Persistence;

/// <summary>
/// A-04: Gönderilmeyi bekleyen e-posta. <see cref="OutboxMessage"/>'ın kardeşi ve aynı gerekçeyle var.
/// <para>
/// E-posta, iş yazımıyla <b>aynı transaction'da</b> kuyruğa alınır ve istek bittikten sonra arka
/// planda gönderilir. Eskiden <c>SmtpEmailSender</c> doğrudan HTTP isteğinin içinden çağrılıyor ve
/// hatayı yeniden fırlatıyordu: SMTP yavaş ya da kapalıysa kullanıcı <b>oluşmuş ve commit edilmiş</b>
/// olmasına rağmen istek 500 dönüyor, kullanıcı tekrar denediğinde <c>duplicate_email</c> alıyordu —
/// çıkışı olmayan bir durum. Push bildirimi bu tedaviyi çoktan almıştı; e-posta almamıştı.
/// </para>
/// </summary>
public sealed class OutgoingEmail
{
    /// <summary>Kayıt kimliği.</summary>
    public Guid Id { get; set; }

    /// <summary>Alıcı adresi.</summary>
    public string ToEmail { get; set; } = string.Empty;

    /// <summary>Konu.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML gövde.</summary>
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>Düz metin gövde.</summary>
    public string TextBody { get; set; } = string.Empty;

    /// <summary>Kuyruğa alınma anı.</summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>Gönderim anı; <c>null</c> ise hâlâ bekliyor.</summary>
    public DateTime? SentOnUtc { get; set; }

    /// <summary>Başarısız deneme sayısı.</summary>
    public int RetryCount { get; set; }

    /// <summary>Bir sonraki denemenin en erken zamanı (üstel backoff).</summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>Son hata metni.</summary>
    public string? Error { get; set; }

    /// <summary>Deneme sınırı aşıldığında işaretlenir; kuyruktan çıkar ama silinmez (incelenmek üzere durur).</summary>
    public DateTime? DeadLetteredOnUtc { get; set; }
}
