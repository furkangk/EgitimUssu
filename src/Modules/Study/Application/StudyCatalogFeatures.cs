using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Application;

// ---- Yanıt DTO'ları ----

public sealed record TopicCatalogResponse(
    Guid Id,
    Guid SubjectId,
    string Name,
    int OrderIndex,
    bool IsActive);

public sealed record SubjectCatalogResponse(
    Guid Id,
    Guid StudentId,
    string Name,
    string? ColorHex,
    bool IsActive,
    IReadOnlyCollection<TopicCatalogResponse> Topics);

// ---- Komut/Sorgu ----

public sealed record ListSubjectCatalogQuery(Guid StudentId)
    : IQuery<Result<IReadOnlyCollection<SubjectCatalogResponse>>>, IStudentScopedRequest;

public sealed record CreateSubjectCatalogCommand(Guid StudentId, string Name, string? ColorHex)
    : ICommand<Result<SubjectCatalogResponse>>, IStudentScopedRequest;

public sealed record UpdateSubjectCatalogCommand(Guid SubjectId, string Name, string? ColorHex, bool IsActive)
    : ICommand<Result<SubjectCatalogResponse>>;

public sealed record DeleteSubjectCatalogCommand(Guid SubjectId) : ICommand<Result<bool>>;

public sealed record AddTopicCatalogCommand(Guid SubjectId, string Name)
    : ICommand<Result<TopicCatalogResponse>>;

public sealed record UpdateTopicCatalogCommand(Guid TopicId, string Name, int OrderIndex, bool IsActive)
    : ICommand<Result<TopicCatalogResponse>>;

public sealed record DeleteTopicCatalogCommand(Guid TopicId) : ICommand<Result<bool>>;

internal static class StudyCatalogErrors
{
    public static readonly Error SubjectNotFound = new("study.subject_not_found", "Ders bulunamadı.");
    public static readonly Error TopicNotFound = new("study.topic_not_found", "Konu bulunamadı.");
    public static readonly Error InvalidRequest = new("study.invalid_request", "Ders/konu bilgileri eksik veya hatalı.");
}

// ---- Mapping ----

internal static class StudyCatalogMappings
{
    public static TopicCatalogResponse ToResponse(this StudentTopicCatalog t) =>
        new(t.Id, t.SubjectId, t.Name, t.OrderIndex, t.IsActive);

    public static SubjectCatalogResponse ToResponse(
        this StudentSubjectCatalog s, IEnumerable<StudentTopicCatalog> topics) =>
        new(
            s.Id,
            s.StudentId,
            s.Name,
            s.ColorHex,
            s.IsActive,
            topics
                .Where(t => t.SubjectId == s.Id)
                .OrderBy(t => t.OrderIndex)
                .ThenBy(t => t.Name)
                .Select(t => t.ToResponse())
                .ToArray());
}

// ---- Handler'lar ----

public sealed class ListSubjectCatalogQueryHandler
    : IQueryHandler<ListSubjectCatalogQuery, Result<IReadOnlyCollection<SubjectCatalogResponse>>>
{
    private readonly IStudyRepository _repository;

    public ListSubjectCatalogQueryHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<SubjectCatalogResponse>>> Handle(
        ListSubjectCatalogQuery query, CancellationToken cancellationToken)
    {
        var subjects = await _repository.ListCatalogSubjectsAsync(query.StudentId, cancellationToken);
        var topics = await _repository.ListCatalogTopicsAsync(query.StudentId, cancellationToken);

        var payload = subjects
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name)
            .Select(s => s.ToResponse(topics))
            .ToArray();

        return Result<IReadOnlyCollection<SubjectCatalogResponse>>.Success(payload);
    }
}

public sealed class CreateSubjectCatalogCommandHandler
    : ICommandHandler<CreateSubjectCatalogCommand, Result<SubjectCatalogResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly StudyLinkResolver _linkResolver;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateSubjectCatalogCommandHandler(
        IStudyRepository repository, StudyLinkResolver linkResolver, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _linkResolver = linkResolver;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<SubjectCatalogResponse>> Handle(
        CreateSubjectCatalogCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<SubjectCatalogResponse>.Failure(StudyCatalogErrors.InvalidRequest);
        }

        await _linkResolver.EnsureAsync(command.StudentId, cancellationToken);

        var subject = new StudentSubjectCatalog(
            _idGenerator.New(), command.StudentId, command.Name, command.ColorHex, _clock.UtcNow);
        await _repository.AddCatalogSubjectAsync(subject, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<SubjectCatalogResponse>.Success(subject.ToResponse(Array.Empty<StudentTopicCatalog>()));
    }
}

public sealed class UpdateSubjectCatalogCommandHandler
    : ICommandHandler<UpdateSubjectCatalogCommand, Result<SubjectCatalogResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IClock _clock;

    public UpdateSubjectCatalogCommandHandler(IStudyRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<SubjectCatalogResponse>> Handle(
        UpdateSubjectCatalogCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<SubjectCatalogResponse>.Failure(StudyCatalogErrors.InvalidRequest);
        }

        var subject = await _repository.GetCatalogSubjectAsync(command.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Result<SubjectCatalogResponse>.Failure(StudyCatalogErrors.SubjectNotFound);
        }

        subject.Update(command.Name, command.ColorHex, command.IsActive, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        var topics = await _repository.ListCatalogTopicsAsync(subject.StudentId, cancellationToken);
        return Result<SubjectCatalogResponse>.Success(subject.ToResponse(topics));
    }
}

public sealed class DeleteSubjectCatalogCommandHandler
    : ICommandHandler<DeleteSubjectCatalogCommand, Result<bool>>
{
    private readonly IStudyRepository _repository;

    public DeleteSubjectCatalogCommandHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeleteSubjectCatalogCommand command, CancellationToken cancellationToken)
    {
        var subject = await _repository.GetCatalogSubjectAsync(command.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Result<bool>.Failure(StudyCatalogErrors.SubjectNotFound);
        }

        // Ders silinince altındaki konular da kaldırılır. Kronometre/test kayıtları ders/konu adını
        // metin olarak kopyaladığından geçmiş veri etkilenmez.
        await _repository.RemoveCatalogSubjectAsync(subject, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class AddTopicCatalogCommandHandler
    : ICommandHandler<AddTopicCatalogCommand, Result<TopicCatalogResponse>>
{
    private readonly IStudyRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public AddTopicCatalogCommandHandler(IStudyRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<TopicCatalogResponse>> Handle(
        AddTopicCatalogCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<TopicCatalogResponse>.Failure(StudyCatalogErrors.InvalidRequest);
        }

        var subject = await _repository.GetCatalogSubjectAsync(command.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Result<TopicCatalogResponse>.Failure(StudyCatalogErrors.SubjectNotFound);
        }

        var existing = await _repository.ListCatalogTopicsBySubjectAsync(subject.Id, cancellationToken);
        var nextOrder = existing.Count == 0 ? 0 : existing.Max(t => t.OrderIndex) + 1;

        var topic = new StudentTopicCatalog(
            _idGenerator.New(), subject.Id, subject.StudentId, command.Name, nextOrder, _clock.UtcNow);
        await _repository.AddCatalogTopicAsync(topic, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<TopicCatalogResponse>.Success(topic.ToResponse());
    }
}

public sealed class UpdateTopicCatalogCommandHandler
    : ICommandHandler<UpdateTopicCatalogCommand, Result<TopicCatalogResponse>>
{
    private readonly IStudyRepository _repository;

    public UpdateTopicCatalogCommandHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<TopicCatalogResponse>> Handle(
        UpdateTopicCatalogCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Result<TopicCatalogResponse>.Failure(StudyCatalogErrors.InvalidRequest);
        }

        var topic = await _repository.GetCatalogTopicAsync(command.TopicId, cancellationToken);
        if (topic is null)
        {
            return Result<TopicCatalogResponse>.Failure(StudyCatalogErrors.TopicNotFound);
        }

        topic.Update(command.Name, command.OrderIndex, command.IsActive);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<TopicCatalogResponse>.Success(topic.ToResponse());
    }
}

public sealed class DeleteTopicCatalogCommandHandler : ICommandHandler<DeleteTopicCatalogCommand, Result<bool>>
{
    private readonly IStudyRepository _repository;

    public DeleteTopicCatalogCommandHandler(IStudyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeleteTopicCatalogCommand command, CancellationToken cancellationToken)
    {
        var topic = await _repository.GetCatalogTopicAsync(command.TopicId, cancellationToken);
        if (topic is null)
        {
            return Result<bool>.Failure(StudyCatalogErrors.TopicNotFound);
        }

        await _repository.RemoveCatalogTopicAsync(topic, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

// ---- Sahiplik yetkilendiricileri (kimlik = subjectId/topicId) ----

public sealed class StudyCatalogSubjectOwnershipAuthorizer :
    ICommandAuthorizer<UpdateSubjectCatalogCommand>,
    ICommandAuthorizer<DeleteSubjectCatalogCommand>,
    ICommandAuthorizer<AddTopicCatalogCommand>
{
    private readonly IStudyRepository _repository;
    private readonly StudyOwnershipGuard _guard;

    public StudyCatalogSubjectOwnershipAuthorizer(IStudyRepository repository, StudyOwnershipGuard guard)
    {
        _repository = repository;
        _guard = guard;
    }

    public Task<Result> Authorize(UpdateSubjectCatalogCommand command, CancellationToken cancellationToken) =>
        AuthorizeSubjectAsync(command.SubjectId, cancellationToken);

    public Task<Result> Authorize(DeleteSubjectCatalogCommand command, CancellationToken cancellationToken) =>
        AuthorizeSubjectAsync(command.SubjectId, cancellationToken);

    public Task<Result> Authorize(AddTopicCatalogCommand command, CancellationToken cancellationToken) =>
        AuthorizeSubjectAsync(command.SubjectId, cancellationToken);

    private async Task<Result> AuthorizeSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var subject = await _repository.GetCatalogSubjectAsync(subjectId, cancellationToken);
        if (subject is null)
        {
            return Result.Failure(StudyCatalogErrors.SubjectNotFound);
        }

        return await _guard.AuthorizeAsync(subject.StudentId, cancellationToken);
    }
}

public sealed class StudyCatalogTopicOwnershipAuthorizer :
    ICommandAuthorizer<UpdateTopicCatalogCommand>,
    ICommandAuthorizer<DeleteTopicCatalogCommand>
{
    private readonly IStudyRepository _repository;
    private readonly StudyOwnershipGuard _guard;

    public StudyCatalogTopicOwnershipAuthorizer(IStudyRepository repository, StudyOwnershipGuard guard)
    {
        _repository = repository;
        _guard = guard;
    }

    public Task<Result> Authorize(UpdateTopicCatalogCommand command, CancellationToken cancellationToken) =>
        AuthorizeTopicAsync(command.TopicId, cancellationToken);

    public Task<Result> Authorize(DeleteTopicCatalogCommand command, CancellationToken cancellationToken) =>
        AuthorizeTopicAsync(command.TopicId, cancellationToken);

    private async Task<Result> AuthorizeTopicAsync(Guid topicId, CancellationToken cancellationToken)
    {
        var topic = await _repository.GetCatalogTopicAsync(topicId, cancellationToken);
        if (topic is null)
        {
            return Result.Failure(StudyCatalogErrors.TopicNotFound);
        }

        return await _guard.AuthorizeAsync(topic.StudentId, cancellationToken);
    }
}
