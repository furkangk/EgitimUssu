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
