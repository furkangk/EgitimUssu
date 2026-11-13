namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>
/// Gönderilecek tek bir e-posta. Sağlayıcıdan bağımsızdır: SMTP/API detayı burada yer almaz.
/// Hem HTML hem düz metin gövde taşınır (istemci hangisini destekliyorsa onu gösterir).
/// </summary>
public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody);
