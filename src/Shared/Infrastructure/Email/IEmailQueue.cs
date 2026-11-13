using EgitimUssu.Shared.Infrastructure.Persistence;

namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>
/// A-04: E-postayı gönderim için <b>kuyruğa alır</b>; ağ çağrısı yapmaz.
/// <para>
/// Kayıt, çağıran modülün <see cref="ModuleDbContext"/>'ine <c>SaveChanges</c> çağrılmadan
/// eklenir — yani iş yazımıyla <b>aynı transaction'da</b> commit olur. İş geri alınırsa e-posta da
/// gitmez; iş yazıldıysa e-posta da mutlaka kuyruktadır. Bu, outbox deseninin e-posta karşılığıdır.
/// </para>
/// </summary>
public interface IEmailQueue
{
    /// <summary>
    /// E-postayı verilen context'e stage eder. <b>SaveChanges çağırmaz</b> — çağıran, kendi iş
    /// yazımıyla birlikte kaydeder.
    /// </summary>
    /// <param name="dbContext">Kuyruğun yazılacağı modül context'i.</param>
    /// <param name="message">Gönderilecek e-posta.</param>
    void Enqueue(ModuleDbContext dbContext, EmailMessage message);
}
