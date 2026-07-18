using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

// ---- Komutlar ----

public sealed record StartStudySessionCommand(Guid StudentId, string Subject, string? Topic)
    : ICommand<Result<StudySessionResponse>>, IStudentScopedRequest;

public sealed record CreateManualStudySessionCommand(
    Guid StudentId,
    string Subject,
    string? Topic,
    int EffectiveMinutes,
    DateTime StudiedOnUtc,
    string? PersonalNote) : ICommand<Result<StudySessionResponse>>, IStudentScopedRequest;

public sealed record PauseStudySessionCommand(Guid SessionId, int? ClientEffectiveMinutes = null)
    : ICommand<Result<StudySessionResponse>>;

public sealed record ResumeStudySessionCommand(Guid SessionId) : ICommand<Result<StudySessionResponse>>;

public sealed record CompleteStudySessionCommand(Guid SessionId, string? PersonalNote, int? ClientEffectiveMinutes = null)
    : ICommand<Result<StudySessionResponse>>;

public sealed record RecoverStudySessionCommand(Guid SessionId, int EffectiveMinutes)
    : ICommand<Result<StudySessionResponse>>;

public sealed record DiscardStudySessionCommand(Guid SessionId) : ICommand<Result<StudySessionResponse>>;

public sealed record EditStudySessionCommand(
    Guid SessionId, string Subject, string? Topic, int EffectiveMinutes, string? PersonalNote)
    : ICommand<Result<StudySessionResponse>>;

public sealed record DeleteStudySessionCommand(Guid SessionId) : ICommand<Result<bool>>;

// ---- Sorgular ----

public sealed record GetStudySessionQuery(Guid SessionId) : IQuery<Result<StudySessionResponse>>;

public sealed record GetActiveSessionQuery(Guid StudentId)
    : IQuery<Result<ActiveSessionResponse?>>, IStudentScopedRequest;

public sealed record ListStudySessionsQuery(Guid StudentId, DateTime? FromUtc, DateTime? ToUtc, string? Subject)
    : IQuery<Result<IReadOnlyCollection<StudySessionResponse>>>, IStudentScopedRequest;

public sealed record WeeklySummaryQuery(Guid StudentId, DateOnly? WeekStart)
    : IQuery<Result<WeeklySummaryResponse>>, IStudentScopedRequest;

internal static class StudyErrors
{
    public static readonly Error SessionNotFound = new("study.session_not_found", "Çalışma seansı bulunamadı.");
    public static readonly Error SessionActive = new("study.session_active", "Zaten devam eden bir çalışma seansınız var. Önce onu tamamlayın.");
    public static readonly Error InvalidRequest = new("study.invalid_request", "Çalışma bilgileri eksik veya hatalı.");
    public static readonly Error InvalidTest = new("study.invalid_test", "Deneme bilgileri geçersiz: doğru + yanlış + boş, toplam soruya eşit olmalı.");
    public static readonly Error GoalNotFound = new("study.goal_not_found", "Çalışma hedefi bulunamadı.");
    public static readonly Error PremiumRequired = new("study.premium_required", "Bu özellik Premium'a özeldir.");
}

/// <summary>
/// Tamamlanan (kronometre veya manuel) seansların ortak sonuç işlemesi:
/// konu rollup + streak + eşik-tabanlı başarım kazanımı.
/// </summary>
public sealed class StudyCompletionService
{
    private readonly IStudyRepository _repository;
    private readonly AchievementEvaluator _evaluator;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public StudyCompletionService(
        IStudyRepository repository,
        AchievementEvaluator evaluator,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _evaluator = evaluator;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task RecordCompletedAsync(StudySession session, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var studiedOn = session.EndedAtUtc ?? now;

        if (!string.IsNullOrWhiteSpace(session.Topic))
        {
            var topic = await _repository.GetTopicAsync(session.StudentId, session.Subject, session.Topic!, cancellationToken);
            if (topic is null)
            {
                await _repository.AddTopicAsync(
                    new StudyTopic(_idGenerator.New(), session.StudentId, session.Subject, session.Topic!, session.EffectiveMinutes, studiedOn),
                    cancellationToken);
            }
            else
            {
                topic.RegisterStudy(session.EffectiveMinutes, studiedOn);
            }
        }

        var streak = await _repository.GetStreakAsync(session.StudentId, cancellationToken);
        if (streak is null)
        {
            streak = new StudyStreak(_idGenerator.New(), session.StudentId, now);
            await _repository.AddStreakAsync(streak, cancellationToken);
        }

        var streakDate = StudyLocalTime.StreakDate(studiedOn);
        var daySessions = await _repository.ListCompletedSessionsAsync(
            session.StudentId,
            StudyLocalTime.LocalDayStartUtc(streakDate),
            StudyLocalTime.LocalDayStartUtc(streakDate.AddDays(1)),
            cancellationToken);
        var dayTotal = daySessions.Sum(s => s.EffectiveMinutes);

        var goal = await _repository.GetActiveGoalAsync(session.StudentId, cancellationToken);
        var thresholdPercent = goal?.StreakThresholdPercent ?? 60;
        var dailyGoal = goal?.DailyGoalMinutes ?? 0;

        if (StreakRules.DayCounts(dayTotal, dailyGoal, thresholdPercent))
        {
            streak.RegisterStudyDay(streakDate, now);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        var metrics = new StudyMetrics(
            streak.CurrentStreakDays,
            await _repository.SumEffectiveMinutesAsync(session.StudentId, cancellationToken),
            await _repository.CountCompletedSessionsAsync(session.StudentId, cancellationToken),
            await _repository.CountTestsAsync(session.StudentId, cancellationToken));

        await _evaluator.EvaluateAsync(session.StudentId, metrics, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class StartStudySessionCommandHandler
    : ICommandHandler<StartStudySessionCommand, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyLinkResolver _linkResolver;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public StartStudySessionCommandHandler(
        IStudyRepository repository, StudyLinkResolver linkResolver, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _linkResolver = linkResolver;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudySessionResponse>> Handle(StartStudySessionCommand command, CancellationToken cancellationToken)
    {
        var active = await _repository.GetActiveSessionAsync(command.StudentId, cancellationToken);
        if (active is not null)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.SessionActive);
        }

        var link = await _linkResolver.EnsureAsync(command.StudentId, cancellationToken);
        var session = StudySession.StartStopwatch(
            _idGenerator.New(),
            command.StudentId,
            command.Subject.Trim(),
            string.IsNullOrWhiteSpace(command.Topic) ? null : command.Topic.Trim(),
            link.ShareStudyWithParent,
            link.ShareStudyWithTeacher,
            _clock.UtcNow);

        await _repository.AddSessionAsync(session, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class CreateManualStudySessionCommandHandler
    : ICommandHandler<CreateManualStudySessionCommand, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyLinkResolver _linkResolver;
    private readonly StudyCompletionService _completion;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateManualStudySessionCommandHandler(
        IStudyRepository repository,
        StudyLinkResolver linkResolver,
        StudyCompletionService completion,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _linkResolver = linkResolver;
        _completion = completion;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudySessionResponse>> Handle(CreateManualStudySessionCommand command, CancellationToken cancellationToken)
    {
        if (command.EffectiveMinutes <= 0 || command.StudiedOnUtc > _clock.UtcNow.AddMinutes(1))
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.InvalidRequest);
        }

        var link = await _linkResolver.EnsureAsync(command.StudentId, cancellationToken);
        var session = StudySession.CreateManual(
            _idGenerator.New(),
            command.StudentId,
            command.Subject.Trim(),
            string.IsNullOrWhiteSpace(command.Topic) ? null : command.Topic.Trim(),
            command.EffectiveMinutes,
            DateTime.SpecifyKind(command.StudiedOnUtc, DateTimeKind.Utc),
            command.PersonalNote?.Trim(),
            link.ShareStudyWithParent,
            link.ShareStudyWithTeacher,
            _clock.UtcNow);

        await _repository.AddSessionAsync(session, cancellationToken);
        await _completion.RecordCompletedAsync(session, cancellationToken);

        return Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class PauseStudySessionCommandHandler
    : ICommandHandler<PauseStudySessionCommand, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public PauseStudySessionCommandHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<StudySessionResponse>> Handle(PauseStudySessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.SessionNotFound);
        }

        session.Pause(_clock.UtcNow, command.ClientEffectiveMinutes);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class ResumeStudySessionCommandHandler
    : ICommandHandler<ResumeStudySessionCommand, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public ResumeStudySessionCommandHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<StudySessionResponse>> Handle(ResumeStudySessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.SessionNotFound);
        }

        session.Resume(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class CompleteStudySessionCommandHandler
    : ICommandHandler<CompleteStudySessionCommand, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyCompletionService _completion;
    private readonly IClock _clock;

    public CompleteStudySessionCommandHandler(
        IStudyRepository repository, StudyCompletionService completion, IClock clock)
    {
        _repository = repository;
        _completion = completion;
        _clock = clock;
    }

    public async Task<Result<StudySessionResponse>> Handle(CompleteStudySessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.SessionNotFound);
        }

        session.Complete(_clock.UtcNow, command.PersonalNote, command.ClientEffectiveMinutes);
        await _completion.RecordCompletedAsync(session, cancellationToken);

        return Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class RecoverStudySessionCommandHandler
    : ICommandHandler<RecoverStudySessionCommand, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyCompletionService _completion;
    private readonly IClock _clock;

    public RecoverStudySessionCommandHandler(
        IStudyRepository repository, StudyCompletionService completion, IClock clock)
    {
        _repository = repository;
        _completion = completion;
        _clock = clock;
    }

    public async Task<Result<StudySessionResponse>> Handle(RecoverStudySessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.SessionNotFound);
        }

        session.RecoverStuck(command.EffectiveMinutes, _clock.UtcNow);
        await _completion.RecordCompletedAsync(session, cancellationToken);

        return Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class DiscardStudySessionCommandHandler
    : ICommandHandler<DiscardStudySessionCommand, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public DiscardStudySessionCommandHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<StudySessionResponse>> Handle(DiscardStudySessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.SessionNotFound);
        }

        session.Discard(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class EditStudySessionCommandHandler
    : ICommandHandler<EditStudySessionCommand, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public EditStudySessionCommandHandler(IStudyRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudySessionResponse>> Handle(EditStudySessionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Subject) || command.EffectiveMinutes <= 0)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.InvalidRequest);
        }

        var session = await _repository.GetSessionAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.SessionNotFound);
        }

        if (session.Status != StudySessionStatus.Completed)
        {
            return Result<StudySessionResponse>.Failure(StudyErrors.InvalidRequest);
        }

        var oldSubject = session.Subject;
        var oldTopic = session.Topic;

        session.EditCompleted(command.Subject, command.Topic, command.EffectiveMinutes, command.PersonalNote, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        await StudyRecompute.RebuildTopicAsync(_repository, _idGenerator, session.StudentId, oldSubject, oldTopic, cancellationToken);
        await StudyRecompute.RebuildTopicAsync(_repository, _idGenerator, session.StudentId, session.Subject, session.Topic, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class DeleteStudySessionCommandHandler : ICommandHandler<DeleteStudySessionCommand, Result<bool>>
{
    private readonly IStudyRepository _repository;
    private readonly IIdGenerator _idGenerator;

    public DeleteStudySessionCommandHandler(IStudyRepository repository, IIdGenerator idGenerator)
    {
        _repository = repository;
        _idGenerator = idGenerator;
    }

    public async Task<Result<bool>> Handle(DeleteStudySessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return Result<bool>.Failure(StudyErrors.SessionNotFound);
        }

        var studentId = session.StudentId;
        var subject = session.Subject;
        var topic = session.Topic;

        _repository.RemoveSession(session);
        await _repository.SaveChangesAsync(cancellationToken);

        await StudyRecompute.RebuildTopicAsync(_repository, _idGenerator, studentId, subject, topic, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

public sealed class GetStudySessionQueryHandler
    : IQueryHandler<GetStudySessionQuery, Result<StudySessionResponse>>
{
    private readonly IStudyRepository _repository;

    public GetStudySessionQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<StudySessionResponse>> Handle(GetStudySessionQuery query, CancellationToken cancellationToken)
    {
        var session = await _repository.GetSessionAsync(query.SessionId, cancellationToken);
        return session is null
            ? Result<StudySessionResponse>.Failure(StudyErrors.SessionNotFound)
            : Result<StudySessionResponse>.Success(session.ToResponse());
    }
}

public sealed class GetActiveSessionQueryHandler
    : IQueryHandler<GetActiveSessionQuery, Result<ActiveSessionResponse?>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public GetActiveSessionQueryHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<ActiveSessionResponse?>> Handle(GetActiveSessionQuery query, CancellationToken cancellationToken)
    {
        var session = await _repository.GetActiveSessionAsync(query.StudentId, cancellationToken);
        return session is null
            ? Result<ActiveSessionResponse?>.Success(null)
            : Result<ActiveSessionResponse?>.Success(new ActiveSessionResponse(session.ToResponse(), session.IsStale(_clock.UtcNow)));
    }
}

public sealed class ListStudySessionsQueryHandler
    : IQueryHandler<ListStudySessionsQuery, Result<IReadOnlyCollection<StudySessionResponse>>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyMembershipResolver _membership;
    private readonly IClock _clock;

    public ListStudySessionsQueryHandler(IStudyRepository repository, StudyMembershipResolver membership, IClock clock)
    {
        _repository = repository;
        _membership = membership;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyCollection<StudySessionResponse>>> Handle(ListStudySessionsQuery query, CancellationToken cancellationToken)
    {
        // Ö-D: Free geçmiş penceresi son 30 güne kısılır; Premium sınırsız.
        var tier = await _membership.CurrentTierAsync(cancellationToken);
        var fromUtc = MembershipGate.ClampFrom(tier, query.FromUtc, _clock.UtcNow);

        var sessions = await _repository.ListSessionsAsync(query.StudentId, fromUtc, query.ToUtc, query.Subject, cancellationToken);
        var payload = sessions.Select(s => s.ToResponse()).ToArray();
        return Result<IReadOnlyCollection<StudySessionResponse>>.Success(payload);
    }
}

public sealed class WeeklySummaryQueryHandler
    : IQueryHandler<WeeklySummaryQuery, Result<WeeklySummaryResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public WeeklySummaryQueryHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<WeeklySummaryResponse>> Handle(WeeklySummaryQuery query, CancellationToken cancellationToken)
    {
        var weekStart = query.WeekStart ?? StartOfWeek(StudyLocalTime.LocalDate(_clock.UtcNow));
        var fromUtc = StudyLocalTime.LocalDayStartUtc(weekStart);
        var toUtc = StudyLocalTime.LocalDayStartUtc(weekStart.AddDays(7));

        var sessions = await _repository.ListCompletedSessionsAsync(query.StudentId, fromUtc, toUtc, cancellationToken);

        var perSubject = sessions
            .GroupBy(s => s.Subject)
            .Select(g => new SubjectMinutesResponse(g.Key, g.Sum(s => s.EffectiveMinutes), g.Count()))
            .OrderByDescending(x => x.EffectiveMinutes)
            .ToArray();

        var perDay = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                var daySessions = sessions.Where(s => StudyLocalTime.LocalDate(s.EndedAtUtc ?? s.StartedAtUtc) == day).ToArray();
                return new DayMinutesResponse(day, daySessions.Sum(s => s.EffectiveMinutes), daySessions.Length);
            })
            .ToArray();

        var summary = new WeeklySummaryResponse(
            weekStart,
            sessions.Sum(s => s.EffectiveMinutes),
            sessions.Sum(s => s.BreakMinutes),
            sessions.Count,
            perSubject,
            perDay);

        return Result<WeeklySummaryResponse>.Success(summary);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        // Hafta Pazartesi başlar.
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }
}
