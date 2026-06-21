using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Shared.Application;
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
    string? TeacherNotes) : ICommand<Result<LessonSessionResponse>>;

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
    DateTime CreatedOnUtc,
    DateTime? CompletedOnUtc);

public interface ILessonSessionRepository
{
    Task<LessonSession?> GetByIdAsync(Guid lessonSessionId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LessonSession>> ListAsync(Guid? teacherUserId, Guid? studentId, DateTime? dateFromUtc, DateTime? dateToUtc, CancellationToken cancellationToken);

    Task AddAsync(LessonSession lessonSession, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ListLessonSessionsQueryHandler : IQueryHandler<ListLessonSessionsQuery, Result<IReadOnlyCollection<LessonSessionResponse>>>
{
    private readonly ILessonSessionRepository _repository;

    public ListLessonSessionsQueryHandler(ILessonSessionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<LessonSessionResponse>>> Handle(ListLessonSessionsQuery query, CancellationToken cancellationToken)
    {
        var sessions = await _repository.ListAsync(query.TeacherUserId, query.StudentId, query.DateFromUtc, query.DateToUtc, cancellationToken);
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

        session.Complete(
            command.ActualStartAtUtc,
            command.ActualEndAtUtc,
            command.AttendanceStatus,
            command.TopicTitle.Trim(),
            command.CoveredContent?.Trim(),
            command.TeacherNotes?.Trim(),
            _clock.UtcNow);

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
            session.CreatedOnUtc,
            session.CompletedOnUtc);
    }
}
