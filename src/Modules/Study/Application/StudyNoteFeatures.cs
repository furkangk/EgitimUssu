using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

// ---- Yanıt DTO ----

public sealed record StudyNoteResponse(
    Guid Id,
    Guid StudentId,
    string Title,
    string Body,
    string? Subject,
    string? Topic,
    string? AttachmentUrl,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

// ---- Komut/Sorgu ----

public sealed record ListStudyNotesQuery(Guid StudentId)
    : IQuery<Result<IReadOnlyCollection<StudyNoteResponse>>>, IStudentScopedRequest;

public sealed record CreateStudyNoteCommand(
    Guid StudentId, string Title, string Body, string? Subject, string? Topic, string? AttachmentUrl)
    : ICommand<Result<StudyNoteResponse>>, IStudentScopedRequest;

public sealed record UpdateStudyNoteCommand(
    Guid NoteId, string Title, string Body, string? Subject, string? Topic, string? AttachmentUrl)
    : ICommand<Result<StudyNoteResponse>>;

public sealed record DeleteStudyNoteCommand(Guid NoteId) : ICommand<Result<bool>>;

internal static class StudyNoteErrors
{
    public static readonly Error NotFound = new("study.note_not_found", "Not bulunamadı.");
    public static readonly Error InvalidRequest = new("study.invalid_request", "Not bilgileri eksik veya hatalı.");
}

internal static class StudyNoteMappings
{
    public static StudyNoteResponse ToResponse(this StudyNote n) => new(
        n.Id, n.StudentId, n.Title, n.Body, n.Subject, n.Topic, n.AttachmentUrl, n.CreatedOnUtc, n.UpdatedOnUtc);
}

// ---- Handler'lar ----

public sealed class ListStudyNotesQueryHandler
    : IQueryHandler<ListStudyNotesQuery, Result<IReadOnlyCollection<StudyNoteResponse>>>
{
    private readonly IStudyRepository _repository;

    public ListStudyNotesQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<StudyNoteResponse>>> Handle(
        ListStudyNotesQuery query, CancellationToken cancellationToken)
    {
        var notes = await _repository.ListNotesAsync(query.StudentId, cancellationToken);
        return Result<IReadOnlyCollection<StudyNoteResponse>>.Success(
            notes.Select(n => n.ToResponse()).ToArray());
    }
}

public sealed class CreateStudyNoteCommandHandler
    : ICommandHandler<CreateStudyNoteCommand, Result<StudyNoteResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyLinkResolver _linkResolver;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateStudyNoteCommandHandler(
        IStudyRepository repository, StudyLinkResolver linkResolver, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _linkResolver = linkResolver;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<StudyNoteResponse>> Handle(CreateStudyNoteCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.Body))
        {
            return Result<StudyNoteResponse>.Failure(StudyNoteErrors.InvalidRequest);
        }

        await _linkResolver.EnsureAsync(command.StudentId, cancellationToken);

        var note = new StudyNote(
            _idGenerator.New(), command.StudentId, command.Title, command.Body,
            command.Subject, command.Topic, command.AttachmentUrl, _clock.UtcNow);
        await _repository.AddNoteAsync(note, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<StudyNoteResponse>.Success(note.ToResponse());
    }
}

public sealed class UpdateStudyNoteCommandHandler
    : ICommandHandler<UpdateStudyNoteCommand, Result<StudyNoteResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public UpdateStudyNoteCommandHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<StudyNoteResponse>> Handle(UpdateStudyNoteCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Title) || string.IsNullOrWhiteSpace(command.Body))
        {
            return Result<StudyNoteResponse>.Failure(StudyNoteErrors.InvalidRequest);
        }

        var note = await _repository.GetNoteAsync(command.NoteId, cancellationToken);
        if (note is null)
        {
            return Result<StudyNoteResponse>.Failure(StudyNoteErrors.NotFound);
        }

        note.Update(command.Title, command.Body, command.Subject, command.Topic, command.AttachmentUrl, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<StudyNoteResponse>.Success(note.ToResponse());
    }
}

public sealed class DeleteStudyNoteCommandHandler : ICommandHandler<DeleteStudyNoteCommand, Result<bool>>
{
    private readonly IStudyRepository _repository;

    public DeleteStudyNoteCommandHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeleteStudyNoteCommand command, CancellationToken cancellationToken)
    {
        var note = await _repository.GetNoteAsync(command.NoteId, cancellationToken);
        if (note is null)
        {
            return Result<bool>.Failure(StudyNoteErrors.NotFound);
        }

        await _repository.RemoveNoteAsync(note, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

/// <summary>Not güncelleme/silme için sahiplik yetkilendiricisi (kimlik = noteId).</summary>
public sealed class StudyNoteOwnershipAuthorizer :
    ICommandAuthorizer<UpdateStudyNoteCommand>,
    ICommandAuthorizer<DeleteStudyNoteCommand>
{
    private readonly IStudyRepository _repository;
    private readonly StudyOwnershipGuard _guard;

    public StudyNoteOwnershipAuthorizer(IStudyRepository repository, StudyOwnershipGuard guard)
    {
        _repository = repository;
        _guard = guard;
    }

    public Task<Result> Authorize(UpdateStudyNoteCommand command, CancellationToken cancellationToken) =>
        AuthorizeNoteAsync(command.NoteId, cancellationToken);

    public Task<Result> Authorize(DeleteStudyNoteCommand command, CancellationToken cancellationToken) =>
        AuthorizeNoteAsync(command.NoteId, cancellationToken);

    private async Task<Result> AuthorizeNoteAsync(Guid noteId, CancellationToken cancellationToken)
    {
        var note = await _repository.GetNoteAsync(noteId, cancellationToken);
        if (note is null)
        {
            return Result.Failure(StudyNoteErrors.NotFound);
        }

        return await _guard.AuthorizeAsync(note.StudentId, cancellationToken);
    }
}
