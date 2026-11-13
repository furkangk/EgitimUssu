using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.LessonSessions.Application;

public sealed record CreateLessonSessionCommand(
    Guid? LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    string Subject,
    DateTime PlannedStartAtUtc,
    string TopicTitle) : ICommand<Result<LessonSessionResponse>>;

public sealed record CompleteLessonSessionCommand(
    Guid LessonSessionId,
    DateTime ActualStartAtUtc,
    DateTime ActualEndAtUtc,
    StudentAttendanceStatus AttendanceStatus,
    string TopicTitle,
    string? CoveredContent,
    string? TeacherNotes,
    bool IsChargeable) : ICommand<Result<LessonSessionResponse>>;

/// <summary>M05-1: Planlı dersi "devam ediyor" durumuna geçirir.</summary>
/// <param name="LessonSessionId">Ders oturumu kimliği.</param>
public sealed record StartLessonSessionCommand(Guid LessonSessionId) : ICommand<Result<LessonSessionResponse>>;

/// <summary>M05-1: Planlı veya devam eden dersi iptal eder.</summary>
/// <param name="LessonSessionId">Ders oturumu kimliği.</param>
/// <param name="Reason">İptal nedeni (opsiyonel).</param>
public sealed record CancelLessonSessionCommand(Guid LessonSessionId, string? Reason) : ICommand<Result<LessonSessionResponse>>;

/// <summary>M05-2: Planlı dersten (M04) ders oturumu türetir. Aynı plan için **idempotent**.</summary>
/// <param name="LessonScheduleId">Planlı ders kimliği.</param>
public sealed record CreateSessionFromScheduleCommand(Guid LessonScheduleId) : ICommand<Result<LessonSessionResponse>>;

public sealed record GetLessonSessionByIdQuery(Guid LessonSessionId) : IQuery<Result<LessonSessionResponse>>;
public sealed record ListLessonSessionsQuery(Guid? TeacherUserId, Guid? StudentId, DateTime? DateFromUtc, DateTime? DateToUtc) : IQuery<Result<IReadOnlyCollection<LessonSessionResponse>>>;

public sealed record LessonSessionResponse(
    Guid Id,
    Guid? LessonScheduleId,
    Guid TeacherUserId,
    Guid StudentId,
    string Subject,
    DateTime PlannedStartAtUtc,
    DateTime? ActualStartAtUtc,
    DateTime? ActualEndAtUtc,
    int? DurationMinutes,
    string AttendanceStatus,
    string Status,
    string TopicTitle,
    string? CoveredContent,
    string? TeacherNotes,
    bool IsChargeable,
    string? MeetingUrl,
    string? RecordingUrl,
    string? CancellationReason,
    DateTime CreatedOnUtc,
    DateTime? CompletedOnUtc);

public interface ILessonSessionRepository
{
    Task<LessonSession?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken);

    /// <summary>M05-2: Bir planlı dersten türetilmiş oturumu döner (idempotency anahtarı).</summary>
    /// <param name="lessonScheduleId">Planlı ders kimliği.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Varsa oturum, yoksa <c>null</c>.</returns>
    Task<LessonSession?> GetByScheduleIdAsync(Guid lessonScheduleId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LessonSession>> ListAsync(Guid? teacherUserId, Guid? studentId, DateTime? dateFromUtc, DateTime? dateToUtc, CancellationToken cancellationToken);

    Task AddAsync(LessonSession lessonSession, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ListLessonSessionsQueryHandler : IQueryHandler<ListLessonSessionsQuery, Result<IReadOnlyCollection<LessonSessionResponse>>>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private readonly ILessonSessionRepository _repository;
    private readonly ICurrentUser _currentUser;

    public ListLessonSessionsQueryHandler(ILessonSessionRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyCollection<LessonSessionResponse>>> Handle(ListLessonSessionsQuery query, CancellationToken cancellationToken)
    {
        // K2: Sahiplik filtresi server tarafında zorlanır; istemci filtresine güvenilmez (varsayılan-deny).
        // Admin tüm kayıtları görebilir; diğer herkes yalnızca kendi kimliğine kapsanır.
        var teacherFilter = query.TeacherUserId;
        var studentFilter = query.StudentId;

        if (!_currentUser.Roles.Contains("Admin"))
        {
            if (!_currentUser.IsAuthenticated || !Guid.TryParse(_currentUser.UserId, out var currentUserId))
            {
                return Result<IReadOnlyCollection<LessonSessionResponse>>.Failure(Forbidden);
            }

            if (_currentUser.Roles.Contains("Teacher"))
            {
                teacherFilter = currentUserId;
            }
            else
            {
                studentFilter = currentUserId;
            }
        }

        var sessions = await _repository.ListAsync(teacherFilter, studentFilter, query.DateFromUtc, query.DateToUtc, cancellationToken);
        return Result<IReadOnlyCollection<LessonSessionResponse>>.Success(sessions.Select(x => x.ToResponse()).ToArray());
    }
}

public sealed class CreateLessonSessionCommandHandler : ICommandHandler<CreateLessonSessionCommand, Result<LessonSessionResponse>>
{
    private readonly ILessonSessionRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateLessonSessionCommandHandler(ILessonSessionRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<LessonSessionResponse>> Handle(CreateLessonSessionCommand command, CancellationToken cancellationToken)
    {
        var session = new LessonSession(
            _idGenerator.New(),
            command.LessonScheduleId,
            command.TeacherUserId,
            command.StudentId,
            command.Subject.Trim(),
            command.PlannedStartAtUtc,
            null,
            null,
            null,
            StudentAttendanceStatus.Unknown,
            LessonSessionStatus.Planned,
            command.TopicTitle.Trim(),
            null,
            null,
            _clock.UtcNow,
            null);

        await _repository.AddAsync(session, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<LessonSessionResponse>.Success(session.ToResponse());
    }
}

public sealed class CompleteLessonSessionCommandHandler : ICommandHandler<CompleteLessonSessionCommand, Result<LessonSessionResponse>>
{
    private static readonly Error NotFound = new("lesson_sessions.not_found", "Ders oturumu bulunamadı.");
    private readonly ILessonSessionRepository _repository;
    private readonly IClock _clock;

    public CompleteLessonSessionCommandHandler(ILessonSessionRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<LessonSessionResponse>> Handle(CompleteLessonSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.LessonSessionId, cancellationToken);
        if (session is null)
        {
            return Result<LessonSessionResponse>.Failure(NotFound);
        }

        // M05-3: durum/zaman kurallari domain'de; ihlalde kayit degismeden hata doner.
        var completion = session.Complete(
            command.ActualStartAtUtc,
            command.ActualEndAtUtc,
            command.AttendanceStatus,
            command.TopicTitle.Trim(),
            command.CoveredContent?.Trim(),
            command.TeacherNotes?.Trim(),
            command.IsChargeable,
            _clock.UtcNow);

        if (completion.IsFailure)
        {
            return Result<LessonSessionResponse>.Failure(completion.Error);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<LessonSessionResponse>.Success(session.ToResponse());
    }
}

/// <summary>M05-1: Dersi başlatma komutunu karşılar.</summary>
public sealed class StartLessonSessionCommandHandler : ICommandHandler<StartLessonSessionCommand, Result<LessonSessionResponse>>
{
    private static readonly Error NotFound = new("lesson_sessions.not_found", "Ders oturumu bulunamadı.");
    private readonly ILessonSessionRepository _repository;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Ders oturumu deposu.</param>
    /// <param name="clock">Saat.</param>
    public StartLessonSessionCommandHandler(ILessonSessionRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Komutu işler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncellenmiş oturum ya da hata.</returns>
    public async Task<Result<LessonSessionResponse>> Handle(StartLessonSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.LessonSessionId, cancellationToken);
        if (session is null)
        {
            return Result<LessonSessionResponse>.Failure(NotFound);
        }

        var transition = session.Start(_clock.UtcNow);
        if (transition.IsFailure)
        {
            return Result<LessonSessionResponse>.Failure(transition.Error);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<LessonSessionResponse>.Success(session.ToResponse());
    }
}

/// <summary>M05-1: Dersi iptal etme komutunu karşılar.</summary>
public sealed class CancelLessonSessionCommandHandler : ICommandHandler<CancelLessonSessionCommand, Result<LessonSessionResponse>>
{
    private static readonly Error NotFound = new("lesson_sessions.not_found", "Ders oturumu bulunamadı.");
    private readonly ILessonSessionRepository _repository;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Ders oturumu deposu.</param>
    /// <param name="clock">Saat.</param>
    public CancelLessonSessionCommandHandler(ILessonSessionRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Komutu işler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Güncellenmiş oturum ya da hata.</returns>
    public async Task<Result<LessonSessionResponse>> Handle(CancelLessonSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.LessonSessionId, cancellationToken);
        if (session is null)
        {
            return Result<LessonSessionResponse>.Failure(NotFound);
        }

        var transition = session.Cancel(command.Reason, _clock.UtcNow);
        if (transition.IsFailure)
        {
            return Result<LessonSessionResponse>.Failure(transition.Error);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<LessonSessionResponse>.Success(session.ToResponse());
    }
}

/// <summary>
/// M05-2: Planlı dersten oturum türetir. Plan bulunamazsa <c>schedule_not_found</c>;
/// <c>Cancelled</c>/<c>Completed</c> plandan türetilemez (<c>schedule_not_derivable</c>).
/// Aynı plan için ikinci çağrı **yeni oturum açmaz**, mevcut oturumu döner.
/// </summary>
public sealed class CreateSessionFromScheduleCommandHandler
    : ICommandHandler<CreateSessionFromScheduleCommand, Result<LessonSessionResponse>>
{
    private static readonly Error ScheduleNotFound =
        new("lesson_sessions.schedule_not_found", "Planlı ders bulunamadı.");

    private static readonly Error ScheduleNotDerivable =
        new("lesson_sessions.schedule_not_derivable", "Bu planlı dersten oturum açılamaz.");

    private readonly ILessonSessionRepository _repository;
    private readonly ILessonScheduleDirectory _scheduleDirectory;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    /// <summary>Yeni örnek oluşturur.</summary>
    /// <param name="repository">Ders oturumu deposu.</param>
    /// <param name="scheduleDirectory">Scheduling okuma sözleşmesi.</param>
    /// <param name="idGenerator">Kimlik üreteci.</param>
    /// <param name="clock">Saat.</param>
    public CreateSessionFromScheduleCommandHandler(
        ILessonSessionRepository repository,
        ILessonScheduleDirectory scheduleDirectory,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _scheduleDirectory = scheduleDirectory;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    /// <summary>Komutu işler.</summary>
    /// <param name="command">Komut.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Türetilen (veya mevcut) oturum.</returns>
    public async Task<Result<LessonSessionResponse>> Handle(CreateSessionFromScheduleCommand command, CancellationToken cancellationToken)
    {
        var schedule = await _scheduleDirectory.GetAsync(command.LessonScheduleId, cancellationToken);
        if (schedule is null)
        {
            return Result<LessonSessionResponse>.Failure(ScheduleNotFound);
        }

        // Idempotency: ayni plan icin ikinci cagri yeni oturum acmaz.
        var existing = await _repository.GetByScheduleIdAsync(command.LessonScheduleId, cancellationToken);
        if (existing is not null)
        {
            return Result<LessonSessionResponse>.Success(existing.ToResponse());
        }

        if (schedule.Status is "Cancelled" or "Completed")
        {
            return Result<LessonSessionResponse>.Failure(ScheduleNotDerivable);
        }

        var session = new LessonSession(
            _idGenerator.New(),
            schedule.LessonScheduleId,
            schedule.TeacherUserId,
            schedule.StudentId,
            schedule.Subject,
            schedule.StartAtUtc,
            null,
            null,
            null,
            StudentAttendanceStatus.Unknown,
            LessonSessionStatus.Planned,
            schedule.Subject,
            null,
            null,
            _clock.UtcNow,
            null);

        session.SetMeetingUrl(schedule.MeetingUrl);

        await _repository.AddAsync(session, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<LessonSessionResponse>.Success(session.ToResponse());
    }
}

public sealed class GetLessonSessionByIdQueryHandler : IQueryHandler<GetLessonSessionByIdQuery, Result<LessonSessionResponse>>
{
    private static readonly Error NotFound = new("lesson_sessions.not_found", "Ders oturumu bulunamadı.");
    private readonly ILessonSessionRepository _repository;

    public GetLessonSessionByIdQueryHandler(ILessonSessionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<LessonSessionResponse>> Handle(GetLessonSessionByIdQuery query, CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(query.LessonSessionId, cancellationToken);
        return session is null
            ? Result<LessonSessionResponse>.Failure(NotFound)
            : Result<LessonSessionResponse>.Success(session.ToResponse());
    }
}

internal static class LessonSessionMappings
{
    public static LessonSessionResponse ToResponse(this LessonSession session)
    {
        return new LessonSessionResponse(
            session.Id,
            session.LessonScheduleId,
            session.TeacherUserId,
            session.StudentId,
            session.Subject,
            session.PlannedStartAtUtc,
            session.ActualStartAtUtc,
            session.ActualEndAtUtc,
            session.DurationMinutes,
            session.AttendanceStatus.ToString(),
            session.Status.ToString(),
            session.TopicTitle,
            session.CoveredContent,
            session.TeacherNotes,
            session.IsChargeable,
            session.MeetingUrl,
            session.RecordingUrl,
            session.CancellationReason,
            session.CreatedOnUtc,
            session.CompletedOnUtc);
    }
}
