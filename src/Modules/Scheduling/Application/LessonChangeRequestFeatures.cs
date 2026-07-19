using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Scheduling.Application;

/// <summary>
/// Öğrenci bir dersin ertelenmesini talep eder (neden + isteğe bağlı alternatif tarih). Öğrenci dersi kendisi
/// değiştirmez; TeacherUserId dersten okunur. Sahiplik <see cref="LessonChangeRequestStudentAuthorizer"/> ile doğrulanır.
/// </summary>
public sealed record CreateLessonChangeRequestCommand(
    Guid StudentId,
    Guid LessonScheduleId,
    string Reason,
    DateTime? ProposedStartAtUtc,
    DateTime? ProposedEndAtUtc) : ICommand<Result<LessonChangeRequestResponse>>;

/// <summary>Öğretmen erteleme talebini kabul eder; alternatif tarih doluysa mevcut Reschedule akışı çalışır.</summary>
public sealed record AcceptLessonChangeRequestCommand(Guid RequestId) : ICommand<Result<LessonChangeRequestResponse>>;

/// <summary>Öğretmen erteleme talebini reddeder; talep kapanır, ders değişmez.</summary>
public sealed record RejectLessonChangeRequestCommand(Guid RequestId) : ICommand<Result<LessonChangeRequestResponse>>;

/// <summary>Öğretmenin erteleme taleplerini listeler; <paramref name="OnlyPending"/> ile yalnızca bekleyenler.</summary>
public sealed record ListLessonChangeRequestsForTeacherQuery(
    Guid TeacherUserId,
    bool OnlyPending) : IQuery<Result<IReadOnlyCollection<LessonChangeRequestResponse>>>;

public sealed record LessonChangeRequestResponse(
    Guid Id,
    Guid LessonScheduleId,
    Guid StudentId,
    Guid TeacherUserId,
    string Reason,
    DateTime? ProposedStartAtUtc,
    DateTime? ProposedEndAtUtc,
    string Status,
    DateTime CreatedOnUtc,
    DateTime? ResolvedOnUtc);

public interface ILessonChangeRequestRepository
{
    Task AddAsync(LessonChangeRequest request, CancellationToken cancellationToken);

    Task<LessonChangeRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonChangeRequest>> ListForTeacherAsync(Guid teacherUserId, bool onlyPending, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LessonChangeRequest>> ListForStudentAsync(Guid studentId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreateLessonChangeRequestCommandValidator : ICommandValidator<CreateLessonChangeRequestCommand>
{
    private static readonly Error InvalidRequest = new("scheduling.invalid_request", "Erteleme talebi bilgileri eksik veya hatalı.");

    public Task<Result> Validate(CreateLessonChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var isValid = command.StudentId != Guid.Empty
            && command.LessonScheduleId != Guid.Empty
            && !string.IsNullOrWhiteSpace(command.Reason);

        return Task.FromResult(isValid ? Result.Success() : Result.Failure(InvalidRequest));
    }
}

public sealed class CreateLessonChangeRequestCommandHandler : ICommandHandler<CreateLessonChangeRequestCommand, Result<LessonChangeRequestResponse>>
{
    private static readonly Error LessonNotFound = new("scheduling.lesson_not_found", "Ders planı bulunamadı.");
    private static readonly Error InvalidRange = new("scheduling.invalid_range", "Önerilen başlangıç ve bitiş aralığı geçersiz.");
    private readonly ILessonChangeRequestRepository _repository;
    private readonly ILessonScheduleRepository _lessonRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public CreateLessonChangeRequestCommandHandler(
        ILessonChangeRequestRepository repository,
        ILessonScheduleRepository lessonRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _lessonRepository = lessonRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<LessonChangeRequestResponse>> Handle(CreateLessonChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _lessonRepository.GetByIdAsync(command.LessonScheduleId, cancellationToken);
        // Ders bulunamazsa veya öğrenciye ait değilse: IDOR sızıntısını önlemek için ayrım yapmadan bulunamadı döneriz.
        if (lesson is null || lesson.StudentId != command.StudentId)
        {
            return Result<LessonChangeRequestResponse>.Failure(LessonNotFound);
        }

        // Alternatif tarih ya tümüyle boş ya da başlangıç+bitiş birlikte ve tutarlı olmalı.
        var hasStart = command.ProposedStartAtUtc.HasValue;
        var hasEnd = command.ProposedEndAtUtc.HasValue;
        if (hasStart != hasEnd || (hasStart && command.ProposedEndAtUtc <= command.ProposedStartAtUtc))
        {
            return Result<LessonChangeRequestResponse>.Failure(InvalidRange);
        }

        var request = new LessonChangeRequest(
            _idGenerator.New(),
            lesson.Id,
            lesson.StudentId,
            lesson.TeacherUserId,
            command.Reason.Trim(),
            command.ProposedStartAtUtc,
            command.ProposedEndAtUtc,
            _clock.UtcNow);

        await _repository.AddAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        // Öğretmene bildirim, LessonChangeRequestedDomainEvent -> outbox -> Notifications handler yoluyla gider.

        return Result<LessonChangeRequestResponse>.Success(request.ToResponse());
    }
}

public sealed class AcceptLessonChangeRequestCommandHandler : ICommandHandler<AcceptLessonChangeRequestCommand, Result<LessonChangeRequestResponse>>
{
    private static readonly Error NotFound = new("scheduling.request_not_found", "Erteleme talebi bulunamadı.");
    private static readonly Error NotPending = new("scheduling.request_not_pending", "Talep zaten sonuçlandırılmış.");
    private readonly ILessonChangeRequestRepository _repository;
    private readonly ICommandDispatcher _dispatcher;
    private readonly IClock _clock;

    public AcceptLessonChangeRequestCommandHandler(
        ILessonChangeRequestRepository repository,
        ICommandDispatcher dispatcher,
        IClock clock)
    {
        _repository = repository;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<Result<LessonChangeRequestResponse>> Handle(AcceptLessonChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result<LessonChangeRequestResponse>.Failure(NotFound);
        }

        if (request.Status != LessonChangeRequestStatus.Pending)
        {
            return Result<LessonChangeRequestResponse>.Failure(NotPending);
        }

        // Alternatif tarih önerildiyse: dersi taşımak için MEVCUT Reschedule akışını yeniden kullan (DRY, çakışma/edit kontrolü dahil).
        // Erteleme başarısızsa talep kabul edilmez; hata olduğu gibi döndürülür.
        if (request.ProposedStartAtUtc is { } start && request.ProposedEndAtUtc is { } end)
        {
            var reschedule = await _dispatcher.Dispatch(
                new RescheduleLessonScheduleCommand(request.LessonScheduleId, start, end, request.Reason),
                cancellationToken);
            if (reschedule.IsFailure)
            {
                return Result<LessonChangeRequestResponse>.Failure(reschedule.Error);
            }
        }

        request.Accept(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<LessonChangeRequestResponse>.Success(request.ToResponse());
    }
}

public sealed class RejectLessonChangeRequestCommandHandler : ICommandHandler<RejectLessonChangeRequestCommand, Result<LessonChangeRequestResponse>>
{
    private static readonly Error NotFound = new("scheduling.request_not_found", "Erteleme talebi bulunamadı.");
    private static readonly Error NotPending = new("scheduling.request_not_pending", "Talep zaten sonuçlandırılmış.");
    private readonly ILessonChangeRequestRepository _repository;
    private readonly IClock _clock;

    public RejectLessonChangeRequestCommandHandler(ILessonChangeRequestRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<LessonChangeRequestResponse>> Handle(RejectLessonChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result<LessonChangeRequestResponse>.Failure(NotFound);
        }

        if (request.Status != LessonChangeRequestStatus.Pending)
        {
            return Result<LessonChangeRequestResponse>.Failure(NotPending);
        }

        request.Reject(_clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<LessonChangeRequestResponse>.Success(request.ToResponse());
    }
}

public sealed class ListLessonChangeRequestsForTeacherQueryHandler : IQueryHandler<ListLessonChangeRequestsForTeacherQuery, Result<IReadOnlyCollection<LessonChangeRequestResponse>>>
{
    private readonly ILessonChangeRequestRepository _repository;

    public ListLessonChangeRequestsForTeacherQueryHandler(ILessonChangeRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyCollection<LessonChangeRequestResponse>>> Handle(ListLessonChangeRequestsForTeacherQuery query, CancellationToken cancellationToken)
    {
        var requests = await _repository.ListForTeacherAsync(query.TeacherUserId, query.OnlyPending, cancellationToken);
        var payload = requests
            .OrderByDescending(request => request.CreatedOnUtc)
            .Select(request => request.ToResponse())
            .ToArray();

        return Result<IReadOnlyCollection<LessonChangeRequestResponse>>.Success(payload);
    }
}

internal static class LessonChangeRequestMappings
{
    public static LessonChangeRequestResponse ToResponse(this LessonChangeRequest request)
    {
        return new LessonChangeRequestResponse(
            request.Id,
            request.LessonScheduleId,
            request.StudentId,
            request.TeacherUserId,
            request.Reason,
            request.ProposedStartAtUtc,
            request.ProposedEndAtUtc,
            request.Status.ToString(),
            request.CreatedOnUtc,
            request.ResolvedOnUtc);
    }
}
