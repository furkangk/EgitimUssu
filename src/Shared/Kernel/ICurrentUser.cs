namespace EgitimUssu.Shared.Kernel;

public interface ICurrentUser
{
    string? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// M01-4: Bu isteği taşıyan <b>oturumun</b> kimliği (erişim jetonundaki <c>sid</c> talebi).
    /// Jetonda yoksa <c>null</c> — cihaz oturumu ekranı bu durumda "bu cihaz" rozetini göstermez
    /// ve toplu iptal fail-closed reddedilir.
    /// <para>
    /// <b>Varsayılan uygulama bilerek <c>null</c> döndürür:</b> bu arayüzü onlarca test sahtesi
    /// uyguluyor ve oturum kimliği yalnız gerçek HTTP bağlamında anlamlı. Varsayılan olmasaydı
    /// tek bir yeni özellik için tüm sahtelerin değişmesi gerekirdi.
    /// </para>
    /// </summary>
    Guid? SessionId => null;
}
