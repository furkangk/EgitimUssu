using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Identity.Domain;

public sealed class UserAccount : AggregateRoot<Guid>
{
    private UserAccount()
    {
    }

    public UserAccount(
        Guid id,
        string email,
        string normalizedEmail,
        string passwordHash,
        string firstName,
        string lastName,
        string? phoneNumber,
        UserAccountStatus status,
        bool isEmailConfirmed,
        bool isProfileVerified,
        DateTime createdOnUtc)
    {
        Id = id;
        Email = email;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Status = status;
        IsEmailConfirmed = isEmailConfirmed;
        IsProfileVerified = isProfileVerified;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;

        Raise(new UserRegisteredDomainEvent(Id, Email, createdOnUtc));
    }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string? PhoneNumber { get; private set; }

    public UserAccountStatus Status { get; private set; }

    public bool IsEmailConfirmed { get; private set; }

    public bool IsProfileVerified { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public List<UserRoleMembership> RoleMemberships { get; private set; } = [];

    public List<RefreshTokenSession> RefreshSessions { get; private set; } = [];

    public List<UserSecurityToken> SecurityTokens { get; private set; } = [];

    public void ConfirmEmail(DateTime now)
    {
        IsEmailConfirmed = true;
        UpdatedOnUtc = now;
    }

    public void UpdatePassword(string passwordHash, DateTime now)
    {
        PasswordHash = passwordHash;
        UpdatedOnUtc = now;
    }

    /// <summary>
    /// E-07: Hesabı kimliksizleştirir ve kapatır.
    /// <para>
    /// Kimlik bilgileri <b>silinmez, üzerine yazılır</b>: satır kalır çünkü ders ve ödeme kayıtları
    /// ona referans verir; kişiyi gösteren her alan kaybolur. Oturumlar ve güvenlik jetonları
    /// tamamen atılır — kimlik bilgisi olarak saklanmalarının artık hiçbir amacı yoktur.
    /// </para>
    /// <para>
    /// <b>İdempotenttir:</b> zaten kapatılmış hesapta çağrılırsa yeni bir vekil adres yazmaz.
    /// </para>
    /// </summary>
    /// <param name="surrogateEmail">
    /// Kaynağıyla <b>ilişkisiz</b> vekil e-posta (<c>PersonalDataPlaceholder.DeletedEmail</c>).
    /// Silinen adresten türetilmiş bir değer (ör. hash) verilmemelidir.
    /// </param>
    /// <param name="unusablePasswordHash">Hiçbir parolayla eşleşmeyen özet.</param>
    /// <param name="nowUtc">Anonimleştirme anı (UTC).</param>
    /// <returns>Bir değişiklik yapıldıysa <c>true</c>.</returns>
    public bool Anonymize(string surrogateEmail, string unusablePasswordHash, DateTime nowUtc)
    {
        if (Status == UserAccountStatus.Closed)
        {
            return false;
        }

        Email = surrogateEmail;
        NormalizedEmail = surrogateEmail.ToUpperInvariant();
        PasswordHash = unusablePasswordHash;
        FirstName = "Silinmiş";
        LastName = "Kullanıcı";
        PhoneNumber = null;
        IsEmailConfirmed = false;
        IsProfileVerified = false;
        Status = UserAccountStatus.Closed;
        UpdatedOnUtc = nowUtc;

        RefreshSessions.Clear();
        SecurityTokens.Clear();
        return true;
    }

    /// <summary>
    /// M15-3: Korunacak oturum dışındaki tüm aktif refresh oturumlarını iptal eder
    /// ("diğer oturumları sonlandır"). Zaten iptal edilmiş kayıtların zaman damgası korunur.
    /// </summary>
    /// <param name="keepSessionId">Ayakta kalacak oturum (genelde az önce açılan).</param>
    /// <param name="nowUtc">İptal zamanı (UTC).</param>
    public void RevokeOtherSessions(Guid keepSessionId, DateTime nowUtc)
    {
        foreach (var session in RefreshSessions)
        {
            if (session.Id != keepSessionId)
            {
                session.Revoke(nowUtc);
            }
        }

        UpdatedOnUtc = nowUtc;
    }
}

public sealed class UserRoleMembership : Entity<Guid>
{
    private UserRoleMembership()
    {
    }

    public UserRoleMembership(Guid id, Guid userAccountId, UserRole role, DateTime assignedOnUtc)
    {
        Id = id;
        UserAccountId = userAccountId;
        Role = role;
        AssignedOnUtc = assignedOnUtc;
    }

    public Guid UserAccountId { get; private set; }

    public UserRole Role { get; private set; }

    public DateTime AssignedOnUtc { get; private set; }
}

public sealed class RefreshTokenSession : Entity<Guid>
{
    private RefreshTokenSession()
    {
    }

    public RefreshTokenSession(
        Guid id,
        Guid userAccountId,
        string refreshTokenHash,
        string? deviceName,
        DateTime createdOnUtc,
        DateTime expiresOnUtc)
    {
        Id = id;
        UserAccountId = userAccountId;
        RefreshTokenHash = refreshTokenHash;
        DeviceName = deviceName;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    public Guid UserAccountId { get; private set; }

    public string RefreshTokenHash { get; private set; } = string.Empty;

    public string? DeviceName { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime? RevokedOnUtc { get; private set; }

    public bool IsActive(DateTime now) => RevokedOnUtc is null && ExpiresOnUtc > now;

    public void Revoke(DateTime revokedOnUtc)
    {
        if (RevokedOnUtc is null)
        {
            RevokedOnUtc = revokedOnUtc;
        }
    }
}

public sealed class UserSecurityToken : Entity<Guid>
{
    private UserSecurityToken()
    {
    }

    public UserSecurityToken(
        Guid id,
        Guid userAccountId,
        SecurityTokenPurpose purpose,
        string tokenHash,
        DateTime createdOnUtc,
        DateTime expiresOnUtc)
    {
        Id = id;
        UserAccountId = userAccountId;
        Purpose = purpose;
        TokenHash = tokenHash;
        CreatedOnUtc = createdOnUtc;
        ExpiresOnUtc = expiresOnUtc;
    }

    public Guid UserAccountId { get; private set; }

    public SecurityTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime ExpiresOnUtc { get; private set; }

    public DateTime? UsedOnUtc { get; private set; }

    public bool IsUsable(DateTime now) => UsedOnUtc is null && ExpiresOnUtc > now;

    public void MarkUsed(DateTime usedOnUtc)
    {
        if (UsedOnUtc is null)
        {
            UsedOnUtc = usedOnUtc;
        }
    }
}

public enum SecurityTokenPurpose
{
    EmailVerification = 1,
    PasswordReset = 2
}

public enum UserAccountStatus
{
    PendingActivation = 1,
    Active = 2,
    Suspended = 3,
    Closed = 4
}

public enum UserRole
{
    Admin = 1,
    Teacher = 2,
    Student = 3,
    Parent = 4
}

public sealed record UserRegisteredDomainEvent(Guid UserId, string Email, DateTime RegisteredOnUtc) : DomainEvent;

/// <summary>
/// E-07: Hesap silme talebinin politikası.
/// <para>
/// Bekleme süresi tek yerde tanımlıdır: silme <b>geri alınamaz</b>, bu yüzden kullanıcıya fikir
/// değiştirme penceresi bırakılır. Süre boyunca hesap kapatılmaz — talep sadece bekler.
/// Dayanak: <c>doc/legal/kvkk_saklama_imha_politikasi.md</c> §3.
/// </para>
/// </summary>
public static class AccountDeletionPolicy
{
    /// <summary>Talep ile uygulama arasındaki bekleme süresi.</summary>
    public static readonly TimeSpan WaitingPeriod = TimeSpan.FromDays(30);
}

/// <summary>
/// E-07: Kullanıcının "hesabımı sil" talebi.
/// <para>
/// Talep bir <b>zamanlayıcıdır</b>, silmenin kendisi değildir: <see cref="ScheduledForUtc"/> gelene
/// kadar hiçbir veri değişmez ve kullanıcı talebi iptal edebilir.
/// </para>
/// </summary>
public sealed class AccountDeletionRequest : AggregateRoot<Guid>
{
    private AccountDeletionRequest()
    {
    }

    /// <summary>Yeni bir silme talebi açar; uygulama anı otomatik olarak 30 gün sonrasıdır.</summary>
    /// <param name="id">Talep kimliği.</param>
    /// <param name="userAccountId">Talebi açan kullanıcı.</param>
    /// <param name="requestedOnUtc">Talep anı (UTC).</param>
    public AccountDeletionRequest(Guid id, Guid userAccountId, DateTime requestedOnUtc)
    {
        Id = id;
        UserAccountId = userAccountId;
        RequestedOnUtc = requestedOnUtc;
        ScheduledForUtc = requestedOnUtc.Add(AccountDeletionPolicy.WaitingPeriod);
        Status = AccountDeletionRequestStatus.Pending;
    }

    public Guid UserAccountId { get; private set; }

    public DateTime RequestedOnUtc { get; private set; }

    /// <summary>Anonimleştirmenin uygulanabileceği en erken an.</summary>
    public DateTime ScheduledForUtc { get; private set; }

    public AccountDeletionRequestStatus Status { get; private set; }

    public DateTime? CancelledOnUtc { get; private set; }

    public DateTime? CompletedOnUtc { get; private set; }

    /// <summary>Talep uygulanabilir mi? İptal edilmiş ya da süresi dolmamışsa <c>false</c>.</summary>
    /// <param name="nowUtc">Şimdiki zaman (UTC).</param>
    public bool IsDue(DateTime nowUtc)
        => Status == AccountDeletionRequestStatus.Pending && nowUtc >= ScheduledForUtc;

    /// <summary>Talebi iptal eder. Zaten iptal/tamamlanmışsa hiçbir şey yapmaz ve <c>false</c> döner.</summary>
    /// <param name="nowUtc">İptal anı (UTC).</param>
    public bool Cancel(DateTime nowUtc)
    {
        if (Status != AccountDeletionRequestStatus.Pending)
        {
            return false;
        }

        Status = AccountDeletionRequestStatus.Cancelled;
        CancelledOnUtc = nowUtc;
        return true;
    }

    /// <summary>Talebi tamamlanmış işaretler (anonimleştirme başarıyla bittikten <b>sonra</b>).</summary>
    /// <param name="nowUtc">Tamamlanma anı (UTC).</param>
    public bool Complete(DateTime nowUtc)
    {
        if (Status != AccountDeletionRequestStatus.Pending)
        {
            return false;
        }

        Status = AccountDeletionRequestStatus.Completed;
        CompletedOnUtc = nowUtc;
        return true;
    }
}

public enum AccountDeletionRequestStatus
{
    Pending = 1,
    Cancelled = 2,
    Completed = 3
}
