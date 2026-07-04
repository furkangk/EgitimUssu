using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

// ---- Komut/Sorgu ----

public sealed record GetStudyGoalsQuery(Guid StudentId) : IQuery<Result<StudyGoalResponse?>>, IStudentScopedRequest;

public sealed record UpdateStudyGoalsCommand(
    Guid StudentId,
    int DailyGoalMinutes,
    int? WeeklyGoalMinutes,
    decimal? TargetNet,
    decimal? TargetScore,
    string? Subject) : ICommand<Result<StudyGoalResponse>>, IStudentScopedRequest;

public sealed record GetStreakQuery(Guid StudentId) : IQuery<Result<StreakResponse>>, IStudentScopedRequest;

public sealed record GetAchievementsQuery(Guid StudentId)
    : IQuery<Result<IReadOnlyCollection<AchievementResponse>>>, IStudentScopedRequest;

public sealed record GetStudySharingQuery(Guid StudentId) : IQuery<Result<StudySharingResponse>>, IStudentScopedRequest;

public sealed record UpdateStudySharingCommand(
    Guid StudentId,
    bool ShareStudyWithParent,
    bool ShareTestsWithParent,
    bool ShareStudyWithTeacher,
    bool ShareTestsWithTeacher) : ICommand<Result<StudySharingResponse>>, IStudentScopedRequest;

public sealed record GetStudyDashboardQuery(Guid StudentId) : IQuery<Result<StudyDashboardResponse>>, IStudentScopedRequest;

// ---- Ortak yardımcı ----

internal static class StudyStatistics
{
    public static async Task<int> TodayEffectiveMinutesAsync(IStudyRepository repository, Guid studentId, DateTime nowUtc, CancellationToken ct)
    {
        var today = StudyLocalTime.LocalDate(nowUtc);
        var fromUtc = StudyLocalTime.LocalDayStartUtc(today);
        var toUtc = StudyLocalTime.LocalDayStartUtc(today.AddDays(1));
        var sessions = await repository.ListCompletedSessionsAsync(studentId, fromUtc, toUtc, ct);
        return sessions.Sum(s => s.EffectiveMinutes);
    }
}

// ---- Handler'lar ----

public sealed class GetStudyGoalsQueryHandler : IQueryHandler<GetStudyGoalsQuery, Result<StudyGoalResponse?>>
{
    private readonly IStudyRepository _repository;

    public GetStudyGoalsQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<StudyGoalResponse?>> Handle(GetStudyGoalsQuery query, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetActiveGoalAsync(query.StudentId, cancellationToken);
        return Result<StudyGoalResponse?>.Success(goal?.ToResponse());
    }
}

public sealed class UpdateStudyGoalsCommandHandler : ICommandHandler<UpdateStudyGoalsCommand, Result<StudyGoalResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyLinkResolver _linkResolver;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public UpdateStudyGoalsCommandHandler(
        IStudyRepository repository, StudyLinkResolver linkResolver, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _linkResolver = linkResolver;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudyGoalResponse>> Handle(UpdateStudyGoalsCommand command, CancellationToken cancellationToken)
    {
        if (command.DailyGoalMinutes < 0)
        {
            return Result<StudyGoalResponse>.Failure(StudyErrors.InvalidRequest);
        }

        await _linkResolver.EnsureAsync(command.StudentId, cancellationToken);
        var now = _clock.UtcNow;
        var subject = string.IsNullOrWhiteSpace(command.Subject) ? null : command.Subject.Trim();

        var goal = await _repository.GetActiveGoalAsync(command.StudentId, cancellationToken);
        if (goal is null)
        {
            goal = new StudyGoal(
                _idGenerator.New(),
                command.StudentId,
                command.DailyGoalMinutes,
                command.WeeklyGoalMinutes,
                command.TargetNet,
                command.TargetScore,
                subject,
                now);
            await _repository.AddGoalAsync(goal, cancellationToken);
        }
        else
        {
            goal.UpdateGoals(command.DailyGoalMinutes, command.WeeklyGoalMinutes, command.TargetNet, command.TargetScore, subject, now);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<StudyGoalResponse>.Success(goal.ToResponse());
    }
}

public sealed class GetStreakQueryHandler : IQueryHandler<GetStreakQuery, Result<StreakResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public GetStreakQueryHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<StreakResponse>> Handle(GetStreakQuery query, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var streak = await _repository.GetStreakAsync(query.StudentId, cancellationToken);
        var todayMinutes = await StudyStatistics.TodayEffectiveMinutesAsync(_repository, query.StudentId, now, cancellationToken);
        var today = StudyLocalTime.LocalDate(now);

        var response = new StreakResponse(
            streak?.CurrentStreakDays ?? 0,
            streak?.LongestStreakDays ?? 0,
            streak?.LastStudiedOnDate,
            streak?.TotalStudyDays ?? 0,
            streak?.LastStudiedOnDate == today,
            todayMinutes);

        return Result<StreakResponse>.Success(response);
    }
}

public sealed class GetAchievementsQueryHandler
    : IQueryHandler<GetAchievementsQuery, Result<IReadOnlyCollection<AchievementResponse>>>
{
    private readonly IStudyRepository _repository;

    public GetAchievementsQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<AchievementResponse>>> Handle(GetAchievementsQuery query, CancellationToken cancellationToken)
    {
        var catalog = await _repository.ListCatalogAsync(cancellationToken);
        var earned = await _repository.ListEarnedAsync(query.StudentId, cancellationToken);
        var streak = await _repository.GetStreakAsync(query.StudentId, cancellationToken);

        var metrics = new StudyMetrics(
            streak?.CurrentStreakDays ?? 0,
            await _repository.SumEffectiveMinutesAsync(query.StudentId, cancellationToken),
            await _repository.CountCompletedSessionsAsync(query.StudentId, cancellationToken),
            await _repository.CountTestsAsync(query.StudentId, cancellationToken));

        var earnedByCode = earned.ToDictionary(e => e.AchievementCode, StringComparer.OrdinalIgnoreCase);

        var payload = catalog
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Threshold)
            .Select(a =>
            {
                earnedByCode.TryGetValue(a.Code, out var earnedRecord);
                return new AchievementResponse(
                    a.Code, a.Title, a.Description, a.Category.ToString(), a.Threshold, a.IconKey,
                    earnedRecord is not null, earnedRecord?.EarnedOnUtc, metrics.ValueFor(a.Category));
            })
            .ToArray();

        return Result<IReadOnlyCollection<AchievementResponse>>.Success(payload);
    }
}

public sealed class GetStudySharingQueryHandler : IQueryHandler<GetStudySharingQuery, Result<StudySharingResponse>>
{
    private readonly IStudyRepository _repository;

    public GetStudySharingQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<StudySharingResponse>> Handle(GetStudySharingQuery query, CancellationToken cancellationToken)
    {
        var link = await _repository.GetLinkAsync(query.StudentId, cancellationToken);
        var response = new StudySharingResponse(
            link?.ShareStudyWithParent ?? false,
            link?.ShareTestsWithParent ?? false,
            link?.ShareStudyWithTeacher ?? false,
            link?.ShareTestsWithTeacher ?? false);
        return Result<StudySharingResponse>.Success(response);
    }
}

public sealed class UpdateStudySharingCommandHandler : ICommandHandler<UpdateStudySharingCommand, Result<StudySharingResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyLinkResolver _linkResolver;
    private readonly IClock _clock;

    public UpdateStudySharingCommandHandler(IStudyRepository repository, StudyLinkResolver linkResolver, IClock clock)
    {
        _repository = repository;
        _linkResolver = linkResolver;
        _clock = clock;
    }

    public async Task<Result<StudySharingResponse>> Handle(UpdateStudySharingCommand command, CancellationToken cancellationToken)
    {
        var link = await _linkResolver.EnsureAsync(command.StudentId, cancellationToken);
        link.UpdateSharing(
            command.ShareStudyWithParent,
            command.ShareTestsWithParent,
            command.ShareStudyWithTeacher,
            command.ShareTestsWithTeacher,
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<StudySharingResponse>.Success(new StudySharingResponse(
            link.ShareStudyWithParent, link.ShareTestsWithParent, link.ShareStudyWithTeacher, link.ShareTestsWithTeacher));
    }
}

public sealed class GetStudyDashboardQueryHandler : IQueryHandler<GetStudyDashboardQuery, Result<StudyDashboardResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public GetStudyDashboardQueryHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<StudyDashboardResponse>> Handle(GetStudyDashboardQuery query, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var today = StudyLocalTime.LocalDate(now);
        var weekStart = StartOfWeek(today);

        var todayMinutes = await StudyStatistics.TodayEffectiveMinutesAsync(_repository, query.StudentId, now, cancellationToken);
        var weekSessions = await _repository.ListCompletedSessionsAsync(
            query.StudentId, StudyLocalTime.LocalDayStartUtc(weekStart), StudyLocalTime.LocalDayStartUtc(weekStart.AddDays(7)), cancellationToken);

        var streak = await _repository.GetStreakAsync(query.StudentId, cancellationToken);
        var goal = await _repository.GetActiveGoalAsync(query.StudentId, cancellationToken);
        var recentSessions = await _repository.ListSessionsAsync(query.StudentId, null, null, null, cancellationToken);
        var tests = await _repository.ListTestsAsync(query.StudentId, null, null, null, null, cancellationToken);

        var catalog = await _repository.ListCatalogAsync(cancellationToken);
        var earned = await _repository.ListEarnedAsync(query.StudentId, cancellationToken);
        var catalogByCode = catalog.ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);

        var recentAchievements = earned
            .OrderByDescending(e => e.EarnedOnUtc)
            .Take(3)
            .Select(e =>
            {
                catalogByCode.TryGetValue(e.AchievementCode, out var a);
                return new AchievementResponse(
                    e.AchievementCode,
                    a?.Title ?? e.AchievementCode,
                    a?.Description ?? string.Empty,
                    a?.Category.ToString() ?? string.Empty,
                    a?.Threshold ?? 0,
                    a?.IconKey,
                    true,
                    e.EarnedOnUtc,
                    e.ProgressValue);
            })
            .ToArray();

        var dailyGoalMinutes = goal?.DailyGoalMinutes ?? 0;
        var lastTest = tests.OrderByDescending(t => t.TakenOnUtc).FirstOrDefault();

        var response = new StudyDashboardResponse(
            query.StudentId,
            todayMinutes,
            dailyGoalMinutes,
            dailyGoalMinutes > 0 && todayMinutes >= dailyGoalMinutes,
            weekSessions.Sum(s => s.EffectiveMinutes),
            streak?.CurrentStreakDays ?? 0,
            streak?.LongestStreakDays ?? 0,
            goal?.ToResponse(),
            lastTest?.ToResponse(),
            recentSessions.Where(s => s.Status == StudySessionStatus.Completed).Take(5).Select(s => s.ToResponse()).ToArray(),
            recentAchievements);

        return Result<StudyDashboardResponse>.Success(response);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }
}

// ---- Seans/test sahiplik yetkilendiricileri (kimlik = sessionId/testResultId) ----

public sealed class StudySessionOwnershipAuthorizer :
    ICommandAuthorizer<PauseStudySessionCommand>,
    ICommandAuthorizer<ResumeStudySessionCommand>,
    ICommandAuthorizer<CompleteStudySessionCommand>,
    ICommandAuthorizer<DiscardStudySessionCommand>,
    IQueryAuthorizer<GetStudySessionQuery>
{
    private readonly IStudyRepository _repository;
    private readonly StudyOwnershipGuard _guard;

    public StudySessionOwnershipAuthorizer(IStudyRepository repository, StudyOwnershipGuard guard)
    {
        _repository = repository;
        _guard = guard;
    }

    public Task<Result> Authorize(PauseStudySessionCommand command, CancellationToken cancellationToken) =>
        AuthorizeSessionAsync(command.SessionId, cancellationToken);

    public Task<Result> Authorize(ResumeStudySessionCommand command, CancellationToken cancellationToken) =>
        AuthorizeSessionAsync(command.SessionId, cancellationToken);

    public Task<Result> Authorize(CompleteStudySessionCommand command, CancellationToken cancellationToken) =>
        AuthorizeSessionAsync(command.SessionId, cancellationToken);

    public Task<Result> Authorize(DiscardStudySessionCommand command, CancellationToken cancellationToken) =>
        AuthorizeSessionAsync(command.SessionId, cancellationToken);

    public Task<Result> Authorize(GetStudySessionQuery query, CancellationToken cancellationToken) =>
        AuthorizeSessionAsync(query.SessionId, cancellationToken);

    private async Task<Result> AuthorizeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure(StudyErrors.SessionNotFound);
        }

        return await _guard.AuthorizeAsync(session.StudentId, cancellationToken);
    }
}

public sealed class StudyTestOwnershipAuthorizer : IQueryAuthorizer<GetTestResultQuery>
{
    private readonly IStudyRepository _repository;
    private readonly StudyOwnershipGuard _guard;

    public StudyTestOwnershipAuthorizer(IStudyRepository repository, StudyOwnershipGuard guard)
    {
        _repository = repository;
        _guard = guard;
    }

    public async Task<Result> Authorize(GetTestResultQuery query, CancellationToken cancellationToken)
    {
        var test = await _repository.GetTestAsync(query.TestResultId, cancellationToken);
        if (test is null)
        {
            return Result.Failure(new Error("study.test_not_found", "Deneme sonucu bulunamadı."));
        }

        return await _guard.AuthorizeAsync(test.StudentId, cancellationToken);
    }
}
