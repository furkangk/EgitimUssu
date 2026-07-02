using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.LessonSessions.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Assignments.Application;

public sealed record AssignmentDraft(
    string Title,
    string? Description,
    DateTime? DueDateUtc,
    string? AttachmentUrl);

public sealed record CreateLessonSessionFollowUpCommand(
    Guid LessonSessionId,
    string Summary,
    string? CoveredTopics,
    string? Recommendations,
    IReadOnlyCollection<AssignmentDraft> Assignments) : ICommand<Result<LessonSessionFollowUpResponse>>;

public sealed record GetLessonSessionFollowUpQuery(Guid LessonSessionId) : IQuery<Result<LessonSessionFollowUpResponse>>;
public sealed record ListAssignmentsQuery(Guid? TeacherUserId, Guid? StudentId, Guid? LessonSessionId) : IQuery<Result<IReadOnlyCollection<AssignmentResponse>>>;

public sealed record AssignmentResponse(
    Guid Id,
    Guid StudentId,
    Guid TeacherUserId,
    Guid? LessonSessionId,
    string Title,
    string? Description,
    DateTime? DueDateUtc,
    string Status,
    string? AttachmentUrl,
    DateTime CreatedOnUtc,
    DateTime? CompletedOnUtc);

public sealed record LessonNoteResponse(
    Guid Id,
    Guid LessonSessionId,
    Guid TeacherUserId,
    Guid StudentId,
    string Summary,
    string? CoveredTopics,
    string? Recommendations,
    DateTime CreatedOnUtc);

public sealed record LessonSessionFollowUpResponse(
    Guid LessonSessionId,
    LessonNoteResponse Note,
    IReadOnlyCollection<AssignmentResponse> Assignments);

public interface IAssignmentRepository
{
    Task<LessonNote?> GetLessonNoteByLessonSessionIdAsync(Guid lessonSessionId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Assignment>> ListByLessonSessionIdAsync(Guid lessonSessionId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Assignment>> ListAsync(Guid? teacherUserId, Guid? studentId, Guid? lessonSessionId, CancellationToken cancellationToken);

    Task AddLessonNoteAsync(LessonNote lessonNote, CancellationToken cancellationToken);

    Task AddAssignmentsAsync(IEnumerable<Assignment> assignments, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class ListAssignmentsQueryHandler : IQueryHandler<ListAssignmentsQuery, Result<IReadOnlyCollection<AssignmentResponse>>>
{
    private static readonly Error Forbidden = new("shared.forbidden", "Bu işlemi yapma yetkiniz yok.");
    private readonly IAssignmentRepository _repository;
    private readonly ICurrentUser _currentUser;

    public ListAssignmentsQueryHandler(IAssignmentRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyCollection<AssignmentResponse>>> Handle(ListAssignmentsQuery query, CancellationToken cancellationToken)
    {
        // K2: Sahiplik filtresi server tarafında zorlanır; istemci filtresine güvenilmez (varsayılan-deny).
        var teacherFilter = query.TeacherUserId;
        var studentFilter = query.StudentId;

        if (!_currentUser.Roles.Contains("Admin"))
        {
            if (!_currentUser.IsAuthenticated || !Guid.TryParse(_currentUser.UserId, out var currentUserId))
            {
                return Result<IReadOnlyCollection<AssignmentResponse>>.Failure(Forbidden);
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

        var assignments = await _repository.ListAsync(teacherFilter, studentFilter, query.LessonSessionId, cancellationToken);
        return Result<IReadOnlyCollection<AssignmentResponse>>.Success(assignments.Select(x => x.ToResponse()).ToArray());
    }
}

public sealed class CreateLessonSessionFollowUpCommandHandler : ICommandHandler<CreateLessonSessionFollowUpCommand, Result<LessonSessionFollowUpResponse>>
{
    private static readonly Error LessonSessionNotFound = new("assignments.lesson_session_not_found", "Ders oturumu bulunamadi.");
    private static readonly Error LessonSessionNotCompleted = new("assignments.lesson_session_not_completed", "Ders oturumu tamamlanmadan not veya odev olusturulamaz.");
    private static readonly Error FollowUpAlreadyExists = new("assignments.follow_up_exists", "Bu ders oturumu icin not ve odev akisi zaten olusturulmus.");
    private readonly IAssignmentRepository _repository;
    private readonly ILessonSessionAccessService _lessonSessionAccessService;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateLessonSessionFollowUpCommandHandler(
        IAssignmentRepository repository,
        ILessonSessionAccessService lessonSessionAccessService,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _lessonSessionAccessService = lessonSessionAccessService;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<LessonSessionFollowUpResponse>> Handle(CreateLessonSessionFollowUpCommand command, CancellationToken cancellationToken)
    {
        var lessonSession = await _lessonSessionAccessService.GetByIdAsync(command.LessonSessionId, cancellationToken);
        if (lessonSession is null)
        {
            return Result<LessonSessionFollowUpResponse>.Failure(LessonSessionNotFound);
        }

        if (!lessonSession.IsCompleted)
        {
            return Result<LessonSessionFollowUpResponse>.Failure(LessonSessionNotCompleted);
        }

        var now = _clock.UtcNow;
        var existingNote = await _repository.GetLessonNoteByLessonSessionIdAsync(command.LessonSessionId, cancellationToken);
        if (existingNote is not null)
        {
            var existingAssignments = await _repository.ListByLessonSessionIdAsync(command.LessonSessionId, cancellationToken);
            if (existingAssignments.Count == 0)
            {
                existingNote.Update(
                    command.Summary.Trim(),
                    command.CoveredTopics?.Trim(),
                    command.Recommendations?.Trim());

                var appendedAssignments = command.Assignments
                    .Select(item => new Assignment(
                        _idGenerator.New(),
                        lessonSession.StudentId,
                        lessonSession.TeacherUserId,
                        command.LessonSessionId,
                        item.Title.Trim(),
                        item.Description?.Trim(),
                        item.DueDateUtc,
                        AssignmentStatus.Pending,
                        item.AttachmentUrl?.Trim(),
                        now,
                        null))
                    .ToArray();

                await _repository.AddAssignmentsAsync(appendedAssignments, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);

                return Result<LessonSessionFollowUpResponse>.Success(new LessonSessionFollowUpResponse(
                    command.LessonSessionId,
                    existingNote.ToResponse(),
                    appendedAssignments.Select(assignment => assignment.ToResponse()).ToArray()));
            }

            return Result<LessonSessionFollowUpResponse>.Failure(FollowUpAlreadyExists);
        }

        var note = new LessonNote(
            _idGenerator.New(),
            command.LessonSessionId,
            lessonSession.TeacherUserId,
            lessonSession.StudentId,
            command.Summary.Trim(),
            command.CoveredTopics?.Trim(),
            command.Recommendations?.Trim(),
            now);

        var assignments = command.Assignments
            .Select(item => new Assignment(
                _idGenerator.New(),
                lessonSession.StudentId,
                lessonSession.TeacherUserId,
                command.LessonSessionId,
                item.Title.Trim(),
                item.Description?.Trim(),
                item.DueDateUtc,
                AssignmentStatus.Pending,
                item.AttachmentUrl?.Trim(),
                now,
                null))
            .ToArray();

        await _repository.AddLessonNoteAsync(note, cancellationToken);
        await _repository.AddAssignmentsAsync(assignments, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<LessonSessionFollowUpResponse>.Success(new LessonSessionFollowUpResponse(
            command.LessonSessionId,
            note.ToResponse(),
            assignments.Select(assignment => assignment.ToResponse()).ToArray()));
    }
}

public sealed class GetLessonSessionFollowUpQueryHandler : IQueryHandler<GetLessonSessionFollowUpQuery, Result<LessonSessionFollowUpResponse>>
{
    private static readonly Error NotFound = new("assignments.follow_up_not_found", "Ders oturumu icin not veya odev bulunamadi.");
    private readonly IAssignmentRepository _repository;
    private readonly ILessonSessionAccessService _lessonSessionAccessService;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public GetLessonSessionFollowUpQueryHandler(
        IAssignmentRepository repository,
        ILessonSessionAccessService lessonSessionAccessService,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _lessonSessionAccessService = lessonSessionAccessService;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<LessonSessionFollowUpResponse>> Handle(GetLessonSessionFollowUpQuery query, CancellationToken cancellationToken)
    {
        var note = await EnsureLessonNoteAsync(query.LessonSessionId, cancellationToken);
        if (note is null)
        {
            return Result<LessonSessionFollowUpResponse>.Failure(NotFound);
        }

        var assignments = await _repository.ListByLessonSessionIdAsync(query.LessonSessionId, cancellationToken);
        return Result<LessonSessionFollowUpResponse>.Success(new LessonSessionFollowUpResponse(
            query.LessonSessionId,
            note.ToResponse(),
            assignments.Select(assignment => assignment.ToResponse()).ToArray()));
    }

    private async Task<LessonNote?> EnsureLessonNoteAsync(Guid lessonSessionId, CancellationToken cancellationToken)
    {
        var note = await _repository.GetLessonNoteByLessonSessionIdAsync(lessonSessionId, cancellationToken);
        if (note is not null)
        {
            return note;
        }

        var lessonSession = await _lessonSessionAccessService.GetByIdAsync(lessonSessionId, cancellationToken);
        if (lessonSession is null || !lessonSession.IsCompleted)
        {
            return null;
        }

        note = new LessonNote(
            _idGenerator.New(),
            lessonSession.Id,
            lessonSession.TeacherUserId,
            lessonSession.StudentId,
            BuildAutoSummary(lessonSession),
            lessonSession.CoveredContent,
            lessonSession.TeacherNotes,
            _clock.UtcNow);

        await _repository.AddLessonNoteAsync(note, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return note;
    }

    private static string BuildAutoSummary(LessonSessionDetails lessonSession)
    {
        if (!string.IsNullOrWhiteSpace(lessonSession.TeacherNotes))
        {
            return lessonSession.TeacherNotes.Trim();
        }

        if (!string.IsNullOrWhiteSpace(lessonSession.CoveredContent))
        {
            return lessonSession.CoveredContent.Trim();
        }

        return $"{lessonSession.TopicTitle.Trim()} konusu tamamlandi.";
    }
}

internal static class AssignmentMappings
{
    public static AssignmentResponse ToResponse(this Assignment assignment)
    {
        return new AssignmentResponse(
            assignment.Id,
            assignment.StudentId,
            assignment.TeacherUserId,
            assignment.LessonSessionId,
            assignment.Title,
            assignment.Description,
            assignment.DueDateUtc,
            assignment.Status.ToString(),
            assignment.AttachmentUrl,
            assignment.CreatedOnUtc,
            assignment.CompletedOnUtc);
    }

    public static LessonNoteResponse ToResponse(this LessonNote note)
    {
        return new LessonNoteResponse(
            note.Id,
            note.LessonSessionId,
            note.TeacherUserId,
            note.StudentId,
            note.Summary,
            note.CoveredTopics,
            note.Recommendations,
            note.CreatedOnUtc);
    }
}
