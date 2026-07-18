using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

/// <summary>
/// Öğrencinin yerel takvim günü hesabı. M15 (Settings) zaman dilimi tercihi devreye girene kadar
/// Türkiye saati (UTC+3) varsayılır.
/// </summary>
public static class StudyLocalTime
{
    public const int OffsetHours = 3;

    public static DateOnly LocalDate(DateTime utcNow) => DateOnly.FromDateTime(utcNow.AddHours(OffsetHours));

    public static DateTime LocalDayStartUtc(DateOnly localDate) =>
        DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue).AddHours(-OffsetHours), DateTimeKind.Utc);

    /// <summary>Streak gün sınırı 04:00'tir (gece geç çalışan öğrenci dünü korur).</summary>
    public static DateOnly StreakDate(DateTime utcNow) => DateOnly.FromDateTime(utcNow.AddHours(OffsetHours).AddHours(-4));
}

/// <summary>Streak (seri) kuralları — birim testli saf mantık.</summary>
public static class StreakRules
{
    public const int MinFixedThresholdMinutes = 20;

    public static int EffectiveThresholdMinutes(int dailyGoalMinutes, int thresholdPercent)
        => dailyGoalMinutes > 0
            ? (int)Math.Ceiling(dailyGoalMinutes * (thresholdPercent / 100.0))
            : MinFixedThresholdMinutes;

    public static bool DayCounts(int dayTotalMinutes, int dailyGoalMinutes, int thresholdPercent)
        => dayTotalMinutes >= EffectiveThresholdMinutes(dailyGoalMinutes, thresholdPercent);
}

/// <summary>
/// Öğrenci-kapsamlı tüm komutları koruyan açık-generik yetkilendirici.
/// Admin her zaman; aksi halde StudyStudent bağı oturum açan kullanıcıya ait olmalı.
/// Bağ henüz yoksa (öğrencinin ilk yazımı) izin verilir; handler bağı oturum kullanıcısına oluşturur.
/// </summary>
public sealed class StudyOwnershipCommandAuthorizer<TCommand> : ICommandAuthorizer<TCommand>
    where TCommand : IStudentScopedRequest
{
    private readonly StudyOwnershipGuard _guard;

    public StudyOwnershipCommandAuthorizer(StudyOwnershipGuard guard)
    {
        _guard = guard;
    }

    public Task<Result> Authorize(TCommand command, CancellationToken cancellationToken) =>
        _guard.AuthorizeAsync(command.StudentId, cancellationToken);
}

/// <summary>Öğrenci-kapsamlı sorguları koruyan açık-generik yetkilendirici.</summary>
public sealed class StudyOwnershipQueryAuthorizer<TQuery> : IQueryAuthorizer<TQuery>
    where TQuery : IStudentScopedRequest
{
    private readonly StudyOwnershipGuard _guard;

    public StudyOwnershipQueryAuthorizer(StudyOwnershipGuard guard)
    {
        _guard = guard;
    }

    public Task<Result> Authorize(TQuery query, CancellationToken cancellationToken) =>
        _guard.AuthorizeAsync(query.StudentId, cancellationToken);
}

public sealed class StudyOwnershipGuard
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu çalışma verisine erişim yetkiniz yok.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudyRepository _repository;

    public StudyOwnershipGuard(ICurrentUser currentUser, IStudyRepository repository)
    {
        _currentUser = currentUser;
        _repository = repository;
    }

    public async Task<Result> AuthorizeAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return Result.Failure(Forbidden);
        }

        if (_currentUser.Roles.Contains("Admin"))
        {
            return Result.Success();
        }

        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Result.Failure(Forbidden);
        }

        var link = await _repository.GetLinkAsync(studentId, cancellationToken);
        if (link is null)
        {
            // Henüz bağ yok: öğrencinin ilk erişimi. Yazım handler'ı bağı oturum kullanıcısına kuracak.
            return Result.Success();
        }

        return link.UserId == userId ? Result.Success() : Result.Failure(Forbidden);
    }
}

/// <summary>
/// Yazım handler'larının çağırdığı yardımcı: bağ yoksa oturum kullanıcısına StudyStudent oluşturur,
/// varsa döndürür. Kayıtların varsayılan paylaşım bayrakları bu bağdan okunur.
/// </summary>
public sealed class StudyLinkResolver
{
    private readonly ICurrentUser _currentUser;
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public StudyLinkResolver(ICurrentUser currentUser, IStudyRepository repository, IClock clock)
    {
        _currentUser = currentUser;
        _repository = repository;
        _clock = clock;
    }

    public async Task<StudyStudent> EnsureAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var link = await _repository.GetLinkAsync(studentId, cancellationToken);
        if (link is not null)
        {
            return link;
        }

        var userId = Guid.TryParse(_currentUser.UserId, out var parsed) ? parsed : Guid.Empty;
        link = new StudyStudent(studentId, userId, _clock.UtcNow);
        await _repository.AddLinkAsync(link, cancellationToken);
        return link;
    }
}

/// <summary>
/// Tamamlanan seans/test sonrası eşik-tabanlı başarım kazanımını değerlendirir.
/// </summary>
public sealed class AchievementEvaluator
{
    private readonly IStudyRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public AchievementEvaluator(IStudyRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task EvaluateAsync(Guid studentId, StudyMetrics metrics, CancellationToken cancellationToken)
    {
        var catalog = await _repository.ListCatalogAsync(cancellationToken);
        if (catalog.Count == 0)
        {
            return;
        }

        var earned = await _repository.ListEarnedAsync(studentId, cancellationToken);
        var earnedCodes = earned.Select(e => e.AchievementCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = _clock.UtcNow;

        foreach (var achievement in catalog)
        {
            if (earnedCodes.Contains(achievement.Code))
            {
                continue;
            }

            var currentValue = metrics.ValueFor(achievement.Category);
            if (currentValue >= achievement.Threshold && achievement.Threshold > 0)
            {
                await _repository.AddEarnedAsync(
                    new StudentAchievement(_idGenerator.New(), studentId, achievement.Id, achievement.Code, currentValue, now),
                    cancellationToken);
                earnedCodes.Add(achievement.Code);
            }
        }
    }
}

public readonly record struct StudyMetrics(
    int CurrentStreakDays,
    int TotalStudyMinutes,
    int CompletedSessionCount,
    int TestCount)
{
    public int ValueFor(AchievementCategory category) => category switch
    {
        AchievementCategory.Streak => CurrentStreakDays,
        AchievementCategory.StudyTime => TotalStudyMinutes,
        AchievementCategory.Consistency => CompletedSessionCount,
        AchievementCategory.TestPerformance => TestCount,
        _ => 0
    };
}

public sealed class StartStudySessionCommandValidator : ICommandValidator<StartStudySessionCommand>
{
    public Task<Result> Validate(StartStudySessionCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(string.IsNullOrWhiteSpace(command.Subject)
            ? Result.Failure(StudyErrors.InvalidRequest)
            : Result.Success());
}

public sealed class CreateManualStudySessionCommandValidator : ICommandValidator<CreateManualStudySessionCommand>
{
    public Task<Result> Validate(CreateManualStudySessionCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(string.IsNullOrWhiteSpace(command.Subject) || command.EffectiveMinutes <= 0
            ? Result.Failure(StudyErrors.InvalidRequest)
            : Result.Success());
}

public sealed class RecordTestResultCommandValidator : ICommandValidator<RecordTestResultCommand>
{
    public Task<Result> Validate(RecordTestResultCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(string.IsNullOrWhiteSpace(command.Subject)
            ? Result.Failure(StudyErrors.InvalidRequest)
            : Result.Success());
}

internal static class StudyMappings
{
    public static StudySessionResponse ToResponse(this StudySession s) => new(
        s.Id, s.StudentId, s.Subject, s.Topic, s.StartedAtUtc, s.EndedAtUtc,
        s.EffectiveMinutes, s.BreakMinutes, s.Status.ToString(), s.Source.ToString(),
        s.PersonalNote, s.IsSharedWithParent, s.IsSharedWithTeacher, s.CreatedOnUtc);

    public static TestResultResponse ToResponse(this TestResult t) => new(
        t.Id, t.StudentId, t.Subject, t.Topic, t.TestName, t.TestType.ToString(),
        t.TotalQuestions, t.Correct, t.Wrong, t.Blank, t.Net, t.PenaltyDivisor,
        t.DurationMinutes, t.TakenOnUtc, t.IsSharedWithParent, t.IsSharedWithTeacher, t.CreatedOnUtc);

    public static StudyGoalResponse ToResponse(this StudyGoal g) => new(
        g.Id, g.StudentId, g.DailyGoalMinutes, g.WeeklyGoalMinutes, g.TargetNet, g.TargetScore,
        g.Subject, g.IsActive, g.UpdatedOnUtc);
}
