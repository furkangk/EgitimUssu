using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.ProgressTracking.Application;

// ---- Sorgu / komut ----

public sealed record ListTopicMasteryQuery(Guid StudentId, string? Subject)
    : IQuery<Result<IReadOnlyCollection<TopicMasteryResponse>>>, IStudentScopedProgressRequest;

public sealed record ListWeakSpotsQuery(Guid StudentId)
    : IQuery<Result<IReadOnlyCollection<TopicMasteryResponse>>>, IStudentScopedProgressRequest;

public sealed record ListStrengthsQuery(Guid StudentId)
    : IQuery<Result<IReadOnlyCollection<TopicMasteryResponse>>>, IStudentScopedProgressRequest;

public sealed record GetProgressOverviewQuery(Guid StudentId)
    : IQuery<Result<ProgressOverviewResponse>>, IStudentScopedProgressRequest;

public sealed record ListTopicGoalsQuery(Guid StudentId, string? Status)
    : IQuery<Result<IReadOnlyCollection<TopicGoalResponse>>>, IStudentScopedProgressRequest;

public sealed record ListProgressSnapshotsQuery(Guid StudentId, string? Subject, string? Period, DateOnly? From, DateOnly? To)
    : IQuery<Result<IReadOnlyCollection<ProgressSnapshotResponse>>>, IStudentScopedProgressRequest;

/// <summary>M10-2: Öğretmenin, bağlı olduğu öğrencinin gelişim özetini okuması (gizlilik filtreli).</summary>
public sealed record GetTeacherStudentProgressOverviewQuery(Guid TeacherUserId, Guid StudentId)
    : IQuery<Result<ProgressOverviewResponse>>;

/// <summary>M10-2: Velinin, onaylı çocuğunun gelişim özetini okuması (gizlilik filtreli).</summary>
public sealed record GetParentChildProgressOverviewQuery(Guid ParentUserId, Guid StudentId)
    : IQuery<Result<ProgressOverviewResponse>>;

public sealed record CreateTopicGoalCommand(
    Guid StudentId, string Subject, string Topic, string TargetMasteryLevel, decimal? TargetNetRatio, DateOnly? TargetDate)
    : ICommand<Result<TopicGoalResponse>>, IStudentScopedProgressRequest;

public sealed record CancelTopicGoalCommand(Guid GoalId) : ICommand<Result<bool>>;

internal static class ProgressErrors
{
    public static readonly Error GoalNotFound = new("progress.goal_not_found", "Konu hedefi bulunamadı.");
    public static readonly Error InvalidRequest = new("progress.invalid_request", "Gelişim/hedef bilgisi eksik veya hatalı.");
}

internal static class ProgressMappings
{
    public static TopicMasteryResponse ToResponse(this TopicMastery m) => new(
        m.Id, m.StudentId, m.Subject, m.Topic, m.MasteryLevel.ToString(), m.MasteryScore,
        m.TotalStudyMinutes, m.TestAttemptCount, m.AverageNetRatio, m.Trend.ToString(),
        m.IsWeakSpot, m.IsStrength, m.LastEvaluatedOnUtc);

    public static ProgressSnapshotResponse ToResponse(this ProgressSnapshot s, ProgressTrend trend) => new(
        s.Id, s.StudentId, s.Subject, s.Period.ToString(), s.PeriodStart, s.MasteryScore,
        s.StudyMinutes, s.TestCount, s.AverageNet, trend.ToString(), s.CreatedOnUtc);

    public static TopicGoalResponse ToResponse(this TopicGoal g) => new(
        g.Id, g.StudentId, g.Subject, g.Topic, g.TargetMasteryLevel.ToString(), g.TargetNetRatio,
        g.SetByRole.ToString(), g.TargetDate, g.Status.ToString(), g.AchievedOnUtc, g.CreatedOnUtc);
}

// ---- Hâkimiyet servisi (consumer'ların çağırdığı ortak upsert + hedef değerlendirme) ----

public sealed class MasteryService
{
    public const string GeneralTopic = "(Genel)";
    private readonly IProgressRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    private readonly ILocalCalendar _calendar;

    public MasteryService(IProgressRepository repository, IIdGenerator idGenerator, IClock clock, ILocalCalendar calendar)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
        _calendar = calendar;
    }

    public async Task ApplyStudyAsync(Guid studentId, string subject, string? topic, int effectiveMinutes, CancellationToken cancellationToken)
    {
        var mastery = await ResolveAsync(studentId, subject, topic, cancellationToken);
        mastery.RegisterStudy(effectiveMinutes, _clock.UtcNow);
        await EvaluateGoalsAsync(mastery, cancellationToken);
    }

    public async Task ApplyTestAsync(Guid studentId, string subject, string? topic, int totalQuestions, decimal net, CancellationToken cancellationToken)
    {
        var mastery = await ResolveAsync(studentId, subject, topic, cancellationToken);
        mastery.RegisterTest(totalQuestions, net, _clock.UtcNow);
        await EvaluateGoalsAsync(mastery, cancellationToken);
    }

    /// <summary>M10-4: Tamamlanan dersten konuya katılım katkısı yazar (M05 beslemesi).</summary>
    public async Task ApplyLessonAttendanceAsync(Guid studentId, string subject, string? topic, int lessonMinutes, CancellationToken cancellationToken)
    {
        var mastery = await ResolveAsync(studentId, subject, topic, cancellationToken);
        mastery.RegisterLessonAttendance(lessonMinutes, _clock.UtcNow);
        await EvaluateGoalsAsync(mastery, cancellationToken);
    }

    public async Task RebuildStudyAsync(Guid studentId, StudyStatistics source, CancellationToken cancellationToken)
    {
        static (string Subject, string Topic) Key(string subject, string? topic)
            => (subject.Trim(), string.IsNullOrWhiteSpace(topic) ? GeneralTopic : topic.Trim());
        var sessions = source.Sessions.GroupBy(x => Key(x.Subject, x.Topic)).ToDictionary(g => g.Key, g => g.Sum(x => x.EffectiveMinutes));
        var tests = source.Tests.GroupBy(x => Key(x.Subject, x.Topic)).ToDictionary(g => g.Key, g => g.ToArray());
        var masteries = (await _repository.ListMasteryAsync(studentId, null, cancellationToken)).ToList();
        foreach (var key in sessions.Keys.Concat(tests.Keys).Distinct())
        {
            if (masteries.Any(x => x.Subject == key.Subject && x.Topic == key.Topic)) continue;
            var mastery = new TopicMastery(_idGenerator.New(), studentId, key.Subject, key.Topic, _clock.UtcNow);
            await _repository.AddMasteryAsync(mastery, cancellationToken);
            masteries.Add(mastery);
        }
        foreach (var mastery in masteries)
        {
            var key = (mastery.Subject, mastery.Topic);
            var attempts = tests.GetValueOrDefault(key) ?? [];
            mastery.RebuildStudy(sessions.GetValueOrDefault(key),
                attempts.Select(x => (x.Id, x.TakenOnUtc, x.TotalQuestions, x.Net)), _clock.UtcNow);
            await EvaluateGoalsAsync(mastery, cancellationToken);
        }
    }

    private async Task<TopicMastery> ResolveAsync(Guid studentId, string subject, string? topic, CancellationToken cancellationToken)
    {
        var normalizedTopic = string.IsNullOrWhiteSpace(topic) ? GeneralTopic : topic.Trim();
        var mastery = await _repository.GetMasteryAsync(studentId, subject.Trim(), normalizedTopic, cancellationToken);
        if (mastery is null)
        {
            mastery = new TopicMastery(_idGenerator.New(), studentId, subject, normalizedTopic, _clock.UtcNow);
            await _repository.AddMasteryAsync(mastery, cancellationToken);
        }

        return mastery;
    }

    private async Task EvaluateGoalsAsync(TopicMastery mastery, CancellationToken cancellationToken)
    {
        var goals = await _repository.ListActiveGoalsForTopicAsync(mastery.StudentId, mastery.Subject, mastery.Topic, cancellationToken);
        foreach (var goal in goals)
        {
            // Tarayıcı henüz koşmamış olsa bile süresi geçen hedef sonradan kazanılamaz.
            goal.MarkMissed(_calendar.ToLocalDate(_clock.UtcNow));
            if (IsGoalMet(mastery, goal))
            {
                // M10-3: Hedef kendiliğinden kapanır ve TopicGoalAchievedDomainEvent yayınlanır
                // (outbox → Notifications, öğrenciye bildirim).
                goal.MarkAchieved(_clock.UtcNow);
            }
        }
    }

    /// <summary>
    /// M10-3: Hedef, <b>hem</b> seviye <b>hem de</b> (verilmişse) net oranı tuttuğunda kapanır.
    /// Net hedefi opsiyoneldir; verilmişse gevşetmez, sıkılaştırır.
    /// </summary>
    private static bool IsGoalMet(TopicMastery mastery, TopicGoal goal) =>
        mastery.MasteryLevel >= goal.TargetMasteryLevel
        && (goal.TargetNetRatio is not { } targetNet || (mastery.AverageNetRatio ?? 0m) >= targetNet);
}

// ---- Handler'lar ----

public sealed class ListTopicMasteryQueryHandler
    : IQueryHandler<ListTopicMasteryQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListTopicMasteryQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TopicMasteryResponse>>> Handle(ListTopicMasteryQuery query, CancellationToken cancellationToken)
    {
        var list = await _repository.ListMasteryAsync(query.StudentId, query.Subject, cancellationToken);
        var payload = list
            .OrderBy(m => m.Subject)
            .ThenByDescending(m => m.MasteryScore)
            .Select(m => m.ToResponse())
            .ToArray();
        return Result<IReadOnlyCollection<TopicMasteryResponse>>.Success(payload);
    }
}

public sealed class ListWeakSpotsQueryHandler
    : IQueryHandler<ListWeakSpotsQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListWeakSpotsQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TopicMasteryResponse>>> Handle(ListWeakSpotsQuery query, CancellationToken cancellationToken)
    {
        var list = await _repository.ListMasteryAsync(query.StudentId, null, cancellationToken);
        var payload = list
            .Where(m => m.IsWeakSpot)
            .OrderBy(m => m.MasteryScore)
            .Select(m => m.ToResponse())
            .ToArray();
        return Result<IReadOnlyCollection<TopicMasteryResponse>>.Success(payload);
    }
}

public sealed class ListStrengthsQueryHandler
    : IQueryHandler<ListStrengthsQuery, Result<IReadOnlyCollection<TopicMasteryResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListStrengthsQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TopicMasteryResponse>>> Handle(ListStrengthsQuery query, CancellationToken cancellationToken)
    {
        var list = await _repository.ListMasteryAsync(query.StudentId, null, cancellationToken);
        var payload = list
            .Where(m => m.IsStrength)
            .OrderByDescending(m => m.MasteryScore)
            .Select(m => m.ToResponse())
            .ToArray();
        return Result<IReadOnlyCollection<TopicMasteryResponse>>.Success(payload);
    }
}

/// <summary>
/// Gelişim özetini kuran ortak okuma yolu. Üç uç (öğrenci kendi, öğretmen, veli) aynı gövdeyi
/// üretir; aralarındaki tek fark <see cref="ProgressPrivacyFilter"/> ile uygulanan gizlilik kapısıdır.
/// </summary>
public sealed class ProgressOverviewReader
{
    private const int DetailTake = 5;
    private readonly IProgressRepository _repository;

    public ProgressOverviewReader(IProgressRepository repository) => _repository = repository;

    public async Task<ProgressOverviewResponse> BuildAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var list = await _repository.ListMasteryAsync(studentId, null, cancellationToken);
        var activeGoals = await _repository.ListGoalsAsync(studentId, TopicGoalStatus.Active, cancellationToken);

        return new ProgressOverviewResponse(
            studentId,
            list.Count(m => m.MasteryLevel == MasteryLevel.Mastered),
            list.Count(m => m.MasteryLevel == MasteryLevel.Proficient),
            list.Count(m => m.MasteryLevel == MasteryLevel.Developing),
            list.Count(m => m.MasteryLevel == MasteryLevel.Weak),
            list.Count(m => m.MasteryLevel == MasteryLevel.NotStarted),
            activeGoals.Count,
            list.Where(m => m.IsWeakSpot).OrderBy(m => m.MasteryScore).Take(DetailTake).Select(m => m.ToResponse()).ToArray(),
            list.Where(m => m.IsStrength).OrderByDescending(m => m.MasteryScore).Take(DetailTake).Select(m => m.ToResponse()).ToArray());
    }
}

public sealed class GetProgressOverviewQueryHandler
    : IQueryHandler<GetProgressOverviewQuery, Result<ProgressOverviewResponse>>
{
    private readonly ProgressOverviewReader _reader;

    public GetProgressOverviewQueryHandler(ProgressOverviewReader reader) => _reader = reader;

    public async Task<Result<ProgressOverviewResponse>> Handle(GetProgressOverviewQuery query, CancellationToken cancellationToken)
        => Result<ProgressOverviewResponse>.Success(await _reader.BuildAsync(query.StudentId, cancellationToken));
}

/// <summary>M10-2: Gelişim özetini kimin okuduğu — gizlilik ayarının hangi bayrağa bakacağını belirler.</summary>
public enum ProgressAudience
{
    Teacher = 1,
    Parent = 2
}

/// <summary>
/// M10-2 / M15-4 gizlilik kapısı. <b>En kısıtlayıcı kazanır</b> (Parents'taki çocuk panosuyla aynı kural):
/// <list type="bullet">
///   <item>İlgili paylaşım bayrağı kapalı <b>veya</b> <c>Hidden</c> → tamamen boş özet.</item>
///   <item><c>Limited</c> → sayaçlar görünür, konu kırılımı (zayıf/güçlü listeleri) gizlenir.</item>
///   <item><c>Standard</c> → tam özet.</item>
/// </list>
/// Öğrencinin kendi ucu bu filtreden geçmez; kendi verisini her zaman tam görür.
/// </summary>
public static class ProgressPrivacyFilter
{
    private static readonly IReadOnlyCollection<TopicMasteryResponse> NoTopics = Array.Empty<TopicMasteryResponse>();

    public static ProgressOverviewResponse Apply(
        ProgressOverviewResponse overview, StudentPrivacy privacy, ProgressAudience audience)
    {
        var isShared = audience == ProgressAudience.Teacher
            ? privacy.ShareStudyDataWithTeacher
            : privacy.ShareStudyDataWithParent;

        if (!isShared || privacy.Level == StudyPrivacyLevel.Hidden)
        {
            return Empty(overview.StudentId);
        }

        return privacy.Level == StudyPrivacyLevel.Limited
            ? overview with { WeakSpots = NoTopics, Strengths = NoTopics }
            : overview;
    }

    public static ProgressOverviewResponse Empty(Guid studentId) =>
        new(studentId, 0, 0, 0, 0, 0, 0, NoTopics, NoTopics);
}

/// <summary>M10-2: Öğretmen ve veli görünümünün ortak gövdesi — okuma + gizlilik filtresi.</summary>
public abstract class SharedProgressOverviewHandler
{
    private readonly ProgressOverviewReader _reader;
    private readonly IStudentDirectory _students;
    private readonly IStudentPrivacyDirectory _privacy;

    protected SharedProgressOverviewHandler(
        ProgressOverviewReader reader, IStudentDirectory students, IStudentPrivacyDirectory privacy)
    {
        _reader = reader;
        _students = students;
        _privacy = privacy;
    }

    protected async Task<Result<ProgressOverviewResponse>> ReadAsync(
        Guid studentId, ProgressAudience audience, CancellationToken cancellationToken)
    {
        var overview = await _reader.BuildAsync(studentId, cancellationToken);

        // Ayar kaydı öğrencinin login kullanıcısına bağlıdır. Bağlı kullanıcı yoksa (öğretmenin
        // elle açtığı, henüz devralınmamış öğrenci) ayar da yoktur → sözleşme gereği paylaşım açık.
        var studentUserId = await _students.GetOwnerUserIdAsync(studentId, cancellationToken);
        if (studentUserId is not { } userId)
        {
            return Result<ProgressOverviewResponse>.Success(overview);
        }

        var privacy = await _privacy.GetForUserAsync(userId, cancellationToken);
        return Result<ProgressOverviewResponse>.Success(ProgressPrivacyFilter.Apply(overview, privacy, audience));
    }
}

public sealed class GetTeacherStudentProgressOverviewQueryHandler
    : SharedProgressOverviewHandler, IQueryHandler<GetTeacherStudentProgressOverviewQuery, Result<ProgressOverviewResponse>>
{
    public GetTeacherStudentProgressOverviewQueryHandler(
        IProgressRepository repository, IStudentDirectory students, IStudentPrivacyDirectory privacy)
        : base(new ProgressOverviewReader(repository), students, privacy)
    {
    }

    public Task<Result<ProgressOverviewResponse>> Handle(
        GetTeacherStudentProgressOverviewQuery query, CancellationToken cancellationToken)
        => ReadAsync(query.StudentId, ProgressAudience.Teacher, cancellationToken);
}

public sealed class GetParentChildProgressOverviewQueryHandler
    : SharedProgressOverviewHandler, IQueryHandler<GetParentChildProgressOverviewQuery, Result<ProgressOverviewResponse>>
{
    public GetParentChildProgressOverviewQueryHandler(
        IProgressRepository repository, IStudentDirectory students, IStudentPrivacyDirectory privacy)
        : base(new ProgressOverviewReader(repository), students, privacy)
    {
    }

    public Task<Result<ProgressOverviewResponse>> Handle(
        GetParentChildProgressOverviewQuery query, CancellationToken cancellationToken)
        => ReadAsync(query.StudentId, ProgressAudience.Parent, cancellationToken);
}

public sealed class ListTopicGoalsQueryHandler
    : IQueryHandler<ListTopicGoalsQuery, Result<IReadOnlyCollection<TopicGoalResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListTopicGoalsQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<TopicGoalResponse>>> Handle(ListTopicGoalsQuery query, CancellationToken cancellationToken)
    {
        TopicGoalStatus? status = Enum.TryParse<TopicGoalStatus>(query.Status, ignoreCase: true, out var parsed) ? parsed : null;
        var list = await _repository.ListGoalsAsync(query.StudentId, status, cancellationToken);
        return Result<IReadOnlyCollection<TopicGoalResponse>>.Success(list.Select(g => g.ToResponse()).ToArray());
    }
}

public sealed class CreateTopicGoalCommandHandler : ICommandHandler<CreateTopicGoalCommand, Result<TopicGoalResponse>>
{
    private readonly IProgressRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateTopicGoalCommandHandler(IProgressRepository repository, ICurrentUser currentUser, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<TopicGoalResponse>> Handle(CreateTopicGoalCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Subject) || string.IsNullOrWhiteSpace(command.Topic)
            || !Enum.TryParse<MasteryLevel>(command.TargetMasteryLevel, ignoreCase: true, out var level))
        {
            return Result<TopicGoalResponse>.Failure(ProgressErrors.InvalidRequest);
        }

        var setBy = Guid.TryParse(_currentUser.UserId, out var userId) ? userId : Guid.Empty;
        var goal = new TopicGoal(
            _idGenerator.New(), command.StudentId, command.Subject, command.Topic, level,
            command.TargetNetRatio, setBy, TopicGoalSetterRole.Student, command.TargetDate, _clock.UtcNow);

        await _repository.AddGoalAsync(goal, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<TopicGoalResponse>.Success(goal.ToResponse());
    }
}

public sealed class CancelTopicGoalCommandHandler : ICommandHandler<CancelTopicGoalCommand, Result<bool>>
{
    private readonly IProgressRepository _repository;

    public CancelTopicGoalCommandHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<bool>> Handle(CancelTopicGoalCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetGoalAsync(command.GoalId, cancellationToken);
        if (goal is null)
        {
            return Result<bool>.Failure(ProgressErrors.GoalNotFound);
        }

        goal.Cancel();
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class ListProgressSnapshotsQueryHandler
    : IQueryHandler<ListProgressSnapshotsQuery, Result<IReadOnlyCollection<ProgressSnapshotResponse>>>
{
    private readonly IProgressRepository _repository;

    public ListProgressSnapshotsQueryHandler(IProgressRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyCollection<ProgressSnapshotResponse>>> Handle(
        ListProgressSnapshotsQuery query, CancellationToken cancellationToken)
    {
        SnapshotPeriod? period = Enum.TryParse<SnapshotPeriod>(query.Period, ignoreCase: true, out var parsed) ? parsed : null;
        var snapshots = await _repository.ListSnapshotsAsync(
            query.StudentId, query.Subject, period, query.From, query.To, cancellationToken);

        // Trend, dönen serideki bir önceki dönemle karşılaştırılarak üretilir; her (ders, dönem türü)
        // kendi serisidir ve serinin ilk öğesinin trendi Stable'dır (karşılaştıracağı dönem yok).
        var payload = snapshots
            .GroupBy(snapshot => (snapshot.Subject, snapshot.Period))
            .OrderBy(series => series.Key.Subject)
            .ThenBy(series => series.Key.Period)
            .SelectMany(series =>
            {
                int? previousScore = null;
                return series
                    .OrderBy(snapshot => snapshot.PeriodStart)
                    .Select(snapshot =>
                    {
                        var trend = ProgressTrendCalculator.FromScores(previousScore, snapshot.MasteryScore);
                        previousScore = snapshot.MasteryScore;
                        return snapshot.ToResponse(trend);
                    })
                    .ToArray();
            })
            .ToArray();

        return Result<IReadOnlyCollection<ProgressSnapshotResponse>>.Success(payload);
    }
}

// ---- Yetkilendirme ----

public sealed class ProgressOwnershipGuard
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu gelişim verisine erişim yetkiniz yok.");
    private readonly ICurrentUser _currentUser;
    private readonly IStudentDirectory _studentDirectory;

    public ProgressOwnershipGuard(ICurrentUser currentUser, IStudentDirectory studentDirectory)
    {
        _currentUser = currentUser;
        _studentDirectory = studentDirectory;
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

        var owner = await _studentDirectory.GetOwnerUserIdAsync(studentId, cancellationToken);
        return owner == userId ? Result.Success() : Result.Failure(Forbidden);
    }
}

public sealed class ProgressOwnershipQueryAuthorizer<TQuery> : IQueryAuthorizer<TQuery>
    where TQuery : IStudentScopedProgressRequest
{
    private readonly ProgressOwnershipGuard _guard;

    public ProgressOwnershipQueryAuthorizer(ProgressOwnershipGuard guard) => _guard = guard;

    public Task<Result> Authorize(TQuery query, CancellationToken cancellationToken) =>
        _guard.AuthorizeAsync(query.StudentId, cancellationToken);
}

public sealed class ProgressOwnershipCommandAuthorizer<TCommand> : ICommandAuthorizer<TCommand>
    where TCommand : IStudentScopedProgressRequest
{
    private readonly ProgressOwnershipGuard _guard;

    public ProgressOwnershipCommandAuthorizer(ProgressOwnershipGuard guard) => _guard = guard;

    public Task<Result> Authorize(TCommand command, CancellationToken cancellationToken) =>
        _guard.AuthorizeAsync(command.StudentId, cancellationToken);
}

/// <summary>Konu hedefi iptali (kimlik = goalId) için sahiplik yetkilendiricisi.</summary>
public sealed class CancelTopicGoalAuthorizer : ICommandAuthorizer<CancelTopicGoalCommand>
{
    private readonly IProgressRepository _repository;
    private readonly ProgressOwnershipGuard _guard;

    public CancelTopicGoalAuthorizer(IProgressRepository repository, ProgressOwnershipGuard guard)
    {
        _repository = repository;
        _guard = guard;
    }

    public async Task<Result> Authorize(CancelTopicGoalCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetGoalAsync(command.GoalId, cancellationToken);
        if (goal is null)
        {
            return Result.Failure(ProgressErrors.GoalNotFound);
        }

        return await _guard.AuthorizeAsync(goal.StudentId, cancellationToken);
    }
}
