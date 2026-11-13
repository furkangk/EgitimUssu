namespace EgitimUssu.Shared.Contracts;

// M15-3: Modüller-arası salt-okunur oturum politikası sözleşmesi.
// Settings uygular (UserSetting.SessionTerminationPolicy); Identity giriş akışında tüketir.
public interface ISessionPolicyDirectory
{
    // userId: giriş yapan kullanıcı. Ayar kaydı yoksa false (KeepLatest) döner — mevcut davranış korunur.
    Task<bool> ShouldTerminateOtherSessionsAsync(Guid userId, CancellationToken cancellationToken);
}
