namespace EgitimUssu.Shared.Infrastructure.Push;

/// <summary>Bir push gönderim denemesinin sonucu.</summary>
public enum PushDeliveryStatus
{
    Delivered = 1,

    /// <summary>Token artık geçerli değil → kalıcı olarak devre dışı bırakılmalı.</summary>
    TokenInvalid = 2,

    /// <summary>Geçici hata → tekrar denenmeli (bildirim "gönderildi" işaretlenmez).</summary>
    TransientFailure = 3
}

/// <summary>Gönderim sonucu; <see cref="Error"/> yalnız başarısızlıkta doludur.</summary>
public sealed record PushResult(PushDeliveryStatus Status, string? Error);
