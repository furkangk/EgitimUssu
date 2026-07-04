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

public sealed record PauseStudySessionCommand(Guid SessionId) : ICommand<Result<StudySessionResponse>>;

public sealed record ResumeStudySessionCommand(Guid SessionId) : ICommand<Result<StudySessionResponse>>;

public sealed record CompleteStudySessionCommand(Guid SessionId, string? PersonalNote)
    : ICommand<Result<StudySessionResponse>>;

public sealed record DiscardStudySessionCommand(Guid SessionId) : ICommand<Result<StudySessionResponse>>;

// ---- Sorgular ----

public sealed record GetStudySessionQuery(Guid SessionId) : IQuery<Result<StudySessionResponse>>;

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

        streak.RegisterStudyDay(StudyLocalTime.LocalDate(studiedOn), now);

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

        session.Pause(_clock.UtcNow);
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

        session.Complete(_clock.UtcNow, command.PersonalNote);
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

public sealed class ListStudySessionsQueryHandler
    : IQueryHandler<ListStudySessionsQuery, Result<IReadOnlyCollection<StudySessionResponse>>>
{
    private readonly IStudyRepository _repository;

    public ListStudySessionsQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<StudySessionResponse>>> Handle(ListStudySessionsQuery query, CancellationToken cancellationToken)
    {
        var sessions = await _repository.ListSessionsAsync(query.StudentId, query.FromUtc, query.ToUtc, query.Subject, cancellationToken);
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
