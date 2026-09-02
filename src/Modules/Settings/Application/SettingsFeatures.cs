using EgitimUssu.Modules.Settings.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Settings.Application;

public sealed record SetStudySharingCommand(Guid UserId, bool ShareWithTeacher, bool ShareWithParent)
    : ICommand<Result<StudySharingResponse>>;

public sealed record StudySharingResponse(Guid UserId, bool ShareWithTeacher, bool ShareWithParent);

public interface IUserSettingRepository
{
    Task<UserSetting?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(UserSetting setting, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class SetStudySharingCommandValidator : ICommandValidator<SetStudySharingCommand>
{
    private static readonly Error Invalid = new("settings.invalid_request", "Ayar bilgileri eksik veya hatalı.");

    public Task<Result> Validate(SetStudySharingCommand command, CancellationToken cancellationToken)
        => Task.FromResult(command.UserId == Guid.Empty ? Result.Failure(Invalid) : Result.Success());
}

public sealed class SetStudySharingCommandHandler : ICommandHandler<SetStudySharingCommand, Result<StudySharingResponse>>
{
    private readonly IUserSettingRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public SetStudySharingCommandHandler(IUserSettingRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudySharingResponse>> Handle(SetStudySharingCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var setting = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (setting is null)
        {
            setting = new UserSetting(
                _idGenerator.New(), command.UserId,
                pushNotificationsEnabled: true, emailNotificationsEnabled: true,
                upcomingLessonReminderEnabled: true, homeworkReminderEnabled: true,
                paymentReminderEnabled: true, weeklySummaryEnabled: true,
                shareStudyDataWithTeacher: command.ShareWithTeacher,
                shareStudyDataWithParent: command.ShareWithParent,
                privacyLevel: PrivacyLevel.Standard,
                sessionTerminationPolicy: SessionTerminationPolicy.KeepLatest,
                lastUpdatedOnUtc: now);
            await _repository.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.SetStudySharing(command.ShareWithTeacher, command.ShareWithParent, now);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<StudySharingResponse>.Success(
            new StudySharingResponse(command.UserId, command.ShareWithTeacher, command.ShareWithParent));
    }
}

// Yalnızca kullanıcının kendisi (veya Admin) kendi ayarını değiştirebilir.
public sealed class SettingsAuthorizer : ICommandAuthorizer<SetStudySharingCommand>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu ayarı değiştirme yetkiniz yok.");
    private readonly ICurrentUser _currentUser;

    public SettingsAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result> Authorize(SetStudySharingCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return Task.FromResult(Result.Failure(Forbidden));
        if (_currentUser.Roles.Contains("Admin")) return Task.FromResult(Result.Success());
        return Task.FromResult(
            Guid.TryParse(_currentUser.UserId, out var uid) && uid == command.UserId
                ? Result.Success() : Result.Failure(Forbidden));
    }
}

// ---------------------------------------------------------------------------
// M15-1: Kullanıcı ayarlarını oku/yaz
// ---------------------------------------------------------------------------

/// <summary>Kullanıcının ayarlarını getirir; kayıt yoksa varsayılan döner.</summary>
/// <param name="UserId">Ayarları istenen kullanıcı.</param>
public sealed record GetUserSettingsQuery(Guid UserId) : IQuery<Result<UserSettingsResponse>>;

/// <summary>Bildirim/gizlilik/güvenlik tercihlerini günceller (upsert).</summary>
/// <param name="UserId">Ayarı güncellenen kullanıcı.</param>
/// <param name="PushNotificationsEnabled">Anlık bildirimler açık mı.</param>
/// <param name="EmailNotificationsEnabled">E-posta bildirimleri açık mı.</param>
/// <param name="UpcomingLessonReminderEnabled">Yaklaşan ders hatırlatması açık mı.</param>
/// <param name="HomeworkReminderEnabled">Ödev hatırlatması açık mı.</param>
/// <param name="PaymentReminderEnabled">Ödeme hatırlatması açık mı.</param>
/// <param name="WeeklySummaryEnabled">Haftalık özet açık mı.</param>
/// <param name="PrivacyLevel">Çalışma verisi gizlilik seviyesi.</param>
/// <param name="SessionTerminationPolicy">Girişte diğer oturumlara ne olacağı.</param>
public sealed record UpdateUserSettingsCommand(
    Guid UserId,
    bool PushNotificationsEnabled,
    bool EmailNotificationsEnabled,
    bool UpcomingLessonReminderEnabled,
    bool HomeworkReminderEnabled,
    bool PaymentReminderEnabled,
    bool WeeklySummaryEnabled,
    PrivacyLevel PrivacyLevel,
    SessionTerminationPolicy SessionTerminationPolicy) : ICommand<Result<UserSettingsResponse>>;

/// <summary>Kullanıcı ayarlarının dış dünyaya açılan görünümü.</summary>
/// <param name="UserId">Kullanıcı kimliği.</param>
/// <param name="PushNotificationsEnabled">Anlık bildirimler açık mı.</param>
/// <param name="EmailNotificationsEnabled">E-posta bildirimleri açık mı.</param>
/// <param name="UpcomingLessonReminderEnabled">Yaklaşan ders hatırlatması açık mı.</param>
/// <param name="HomeworkReminderEnabled">Ödev hatırlatması açık mı.</param>
/// <param name="PaymentReminderEnabled">Ödeme hatırlatması açık mı.</param>
/// <param name="WeeklySummaryEnabled">Haftalık özet açık mı.</param>
/// <param name="ShareStudyDataWithTeacher">Çalışma verisi öğretmenle paylaşılıyor mu.</param>
/// <param name="ShareStudyDataWithParent">Çalışma verisi veliyle paylaşılıyor mu.</param>
/// <param name="PrivacyLevel">Gizlilik seviyesi adı.</param>
/// <param name="SessionTerminationPolicy">Oturum sonlandırma politikası adı.</param>
/// <param name="LastUpdatedOnUtc">Son güncelleme zamanı (UTC).</param>
public sealed record UserSettingsResponse(
    Guid UserId,
    bool PushNotificationsEnabled,
    bool EmailNotificationsEnabled,
    bool UpcomingLessonReminderEnabled,
    bool HomeworkReminderEnabled,
    bool PaymentReminderEnabled,
    bool WeeklySummaryEnabled,
    bool ShareStudyDataWithTeacher,
    bool ShareStudyDataWithParent,
    string PrivacyLevel,
    string SessionTerminationPolicy,
    DateTime LastUpdatedOnUtc);

internal static class UserSettingsMapper
{
    public static UserSettingsResponse ToResponse(this UserSetting setting)
        => new(
            setting.UserId,
            setting.PushNotificationsEnabled,
            setting.EmailNotificationsEnabled,
            setting.UpcomingLessonReminderEnabled,
            setting.HomeworkReminderEnabled,
            setting.PaymentReminderEnabled,
            setting.WeeklySummaryEnabled,
            setting.ShareStudyDataWithTeacher,
            setting.ShareStudyDataWithParent,
            setting.PrivacyLevel.ToString(),
            setting.SessionTerminationPolicy.ToString(),
            setting.LastUpdatedOnUtc);
}

/// <summary>Ayar okuma sorgusunu karşılar; kayıt yoksa varsayılan nesneyi döner (404 değil).</summary>
public sealed class GetUserSettingsQueryHandler : IQueryHandler<GetUserSettingsQuery, Result<UserSettingsResponse>>
{
    private readonly IUserSettingRepository _repository;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Ayar deposu.</param>
    /// <param name="clock">Saat.</param>
    public GetUserSettingsQueryHandler(IUserSettingRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Sorguyu işler.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Kullanıcı ayarları.</returns>
    public async Task<Result<UserSettingsResponse>> Handle(GetUserSettingsQuery query, CancellationToken cancellationToken)
    {
        var setting = await _repository.GetByUserIdAsync(query.UserId, cancellationToken)
            ?? UserSetting.Default(query.UserId, _clock.UtcNow);

        return Result<UserSettingsResponse>.Success(setting.ToResponse());
    }
}

/// <summary>Ayar güncelleme komutunu karşılar (kayıt yoksa oluşturur).</summary>
public sealed class UpdateUserSettingsCommandHandler : ICommandHandler<UpdateUserSettingsCommand, Result<UserSettingsResponse>>
{
    private readonly IUserSettingRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Ayar deposu.</param>
    /// <param name="idGenerator">Kimlik üreteci.</param>
    /// <param name="clock">Saat.</param>
    public UpdateUserSettingsCommandHandler(IUserSettingRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    /// <summary>Komutu işler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncellenmiş ayarlar.</returns>
    public async Task<Result<UserSettingsResponse>> Handle(UpdateUserSettingsCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var setting = await _repository.GetByUserIdAsync(command.UserId, cancellationToken);
        if (setting is null)
        {
            // Kayıt yoksa oluştur; çalışma paylaşımı varsayılanı (kapalı) korunur.
            setting = new UserSetting(
                _idGenerator.New(), command.UserId,
                command.PushNotificationsEnabled, command.EmailNotificationsEnabled,
                command.UpcomingLessonReminderEnabled, command.HomeworkReminderEnabled,
                command.PaymentReminderEnabled, command.WeeklySummaryEnabled,
                shareStudyDataWithTeacher: false, shareStudyDataWithParent: false,
                command.PrivacyLevel, command.SessionTerminationPolicy, now);
            await _repository.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.UpdatePreferences(
                command.PushNotificationsEnabled, command.EmailNotificationsEnabled,
                command.UpcomingLessonReminderEnabled, command.HomeworkReminderEnabled,
                command.PaymentReminderEnabled, command.WeeklySummaryEnabled,
                command.PrivacyLevel, command.SessionTerminationPolicy, now);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<UserSettingsResponse>.Success(setting.ToResponse());
    }
}

/// <summary>Ayar güncelleme komutunun temel doğrulaması.</summary>
public sealed class UpdateUserSettingsCommandValidator : ICommandValidator<UpdateUserSettingsCommand>
{
    private static readonly Error Invalid = new("settings.invalid_request", "Ayar bilgileri eksik veya hatalı.");

    /// <summary>Komutu doğrular.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Doğrulama sonucu.</returns>
    public Task<Result> Validate(UpdateUserSettingsCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == Guid.Empty) return Task.FromResult(Result.Failure(Invalid));
        if (!Enum.IsDefined(command.PrivacyLevel)) return Task.FromResult(Result.Failure(Invalid));
        if (!Enum.IsDefined(command.SessionTerminationPolicy)) return Task.FromResult(Result.Failure(Invalid));
        return Task.FromResult(Result.Success());
    }
}

// M15-2: Sahiplik — kullanıcı yalnız kendi ayarını okur/yazar, Admin istisnadır.
internal static class SettingsOwnership
{
    public static readonly Error Forbidden = new("shared.forbidden", "Bu ayara erişim yetkiniz yok.");

    public static Result Check(ICurrentUser currentUser, Guid targetUserId)
    {
        if (!currentUser.IsAuthenticated) return Result.Failure(Forbidden);
        if (currentUser.Roles.Contains("Admin")) return Result.Success();
        return Guid.TryParse(currentUser.UserId, out var uid) && uid == targetUserId
            ? Result.Success()
            : Result.Failure(Forbidden);
    }
}

/// <summary>Ayar okuma yetkisi: yalnız kullanıcının kendisi veya Admin.</summary>
public sealed class GetUserSettingsQueryAuthorizer : IQueryAuthorizer<GetUserSettingsQuery>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    public GetUserSettingsQueryAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Yetkiyi denetler.</summary>
    /// <param name="query">Sorgu.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(GetUserSettingsQuery query, CancellationToken cancellationToken)
        => Task.FromResult(SettingsOwnership.Check(_currentUser, query.UserId));
}

/// <summary>Ayar yazma yetkisi: yalnız kullanıcının kendisi veya Admin.</summary>
public sealed class UpdateUserSettingsCommandAuthorizer : ICommandAuthorizer<UpdateUserSettingsCommand>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="currentUser">Oturumdaki kullanıcı.</param>
    public UpdateUserSettingsCommandAuthorizer(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>Yetkiyi denetler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Yetki sonucu.</returns>
    public Task<Result> Authorize(UpdateUserSettingsCommand command, CancellationToken cancellationToken)
        => Task.FromResult(SettingsOwnership.Check(_currentUser, command.UserId));
}
