namespace EgitimUssu.Shared.Application;

/// <summary>
/// Dispatcher düzeyinde authorizer gerektirmeyen command/query tiplerini işaretler.
/// Bu marker uygulandığında ICommandAuthorizer/IQueryAuthorizer kaydı zorunlu olmaktan çıkar.
///
/// Kullanım kararı açıkça belgelenmeli:
///   - Gerçek anonymous (login, register, şifre sıfırlama) → her zaman uygundur.
///   - "HTTP katmanı auth'u zaten sağlıyor, ek rol/sahiplik kontrolü yok" → geçici/bilinçli kullanım;
///     ilerleyen sürümde uygun bir ICommandAuthorizer/IQueryAuthorizer ile değiştirilmeli.
/// </summary>
public interface IAllowAnonymous;
