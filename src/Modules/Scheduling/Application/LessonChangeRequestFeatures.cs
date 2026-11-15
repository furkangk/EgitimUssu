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
    DateTime? ProposedEndAtUtc,
    DateTime? OccurrenceStartAtUtc = null) : ICommand<Result<LessonChangeRequestResponse>>;

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
    DateTime? OccurrenceStartAtUtc,
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
    private static readonly Error OccurrenceRequired = new(
        "scheduling.invalid_request", "Tekrarlı derste ertelenecek dersin tarihi (occurrenceStartAtUtc) zorunlu.");
    private readonly ILessonChangeRequestRepository _repository;
    private readonly ILessonScheduleRepository _lessonRepository;
    private readonly ILocalCalendar _calendar;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly ITeacherScheduleLock _scheduleLock;

    public CreateLessonChangeRequestCommandHandler(
        ILessonChangeRequestRepository repository,
        ILessonScheduleRepository lessonRepository,
        ILocalCalendar calendar,
        IIdGenerator idGenerator,
        IClock clock,
        ITeacherScheduleLock scheduleLock)
    {
        _scheduleLock = scheduleLock;
        _repository = repository;
        _lessonRepository = lessonRepository;
        _calendar = calendar;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result<LessonChangeRequestResponse>> Handle(CreateLessonChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var initial = await _lessonRepository.GetByIdAsync(command.LessonScheduleId, cancellationToken);
        if (initial?.TeacherUserId is not { } teacher) return Result<LessonChangeRequestResponse>.Failure(LessonNotFound);
        return await _scheduleLock.RunExclusiveAsync(teacher, token => HandleLocked(command, token), cancellationToken);
    }

    private async Task<Result<LessonChangeRequestResponse>> HandleLocked(CreateLessonChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _lessonRepository.GetByIdAsync(command.LessonScheduleId, cancellationToken);
        // Ders bulunamazsa veya öğrenciye ait değilse: IDOR sızıntısını önlemek için ayrım yapmadan bulunamadı döneriz.
        // Öğretmensiz (self) ders için erteleme talebi anlamsızdır (öğrenci kendi dersini doğrudan düzenler).
        if (lesson is null || lesson.StudentId != command.StudentId || lesson.TeacherUserId is null)
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

        // P17 T1 / N-01: talep hangi oluşumu ertelediğini taşır — kabul yalnız onu taşır (eskiden bütün seri kayıyordu).
        // Tekrarlı derste anahtar zorunlu ve serinin orijinal oluşumu olmalı; tekil derste dersin başlangıcı.
        DateTime occurrenceStartAtUtc;
        if (string.IsNullOrWhiteSpace(lesson.RecurrenceRule))
        {
            occurrenceStartAtUtc = lesson.StartAtUtc;
        }
        else if (command.OccurrenceStartAtUtc is not { } key)
        {
            return Result<LessonChangeRequestResponse>.Failure(OccurrenceRequired);
        }
        else if (!OccurrenceScopeRules.IsOccurrence(lesson, key, _calendar))
        {
            return Result<LessonChangeRequestResponse>.Failure(OccurrenceScopeRules.OccurrenceNotFound);
        }
        else
        {
            occurrenceStartAtUtc = key;
        }

        var activeError = await ChangeRequestOccurrenceRules.ValidateAsync(_lessonRepository, lesson, occurrenceStartAtUtc, _calendar, _clock.UtcNow, cancellationToken);
        if (activeError is not null) return Result<LessonChangeRequestResponse>.Failure(activeError);
        if (command.ProposedStartAtUtc <= _clock.UtcNow) return Result<LessonChangeRequestResponse>.Failure(InvalidRange);
        var pending = await _repository.ListForTeacherAsync(lesson.TeacherUserId.Value, true, cancellationToken);
        if (pending.Any(r => r.LessonScheduleId == lesson.Id && r.OccurrenceStartAtUtc == occurrenceStartAtUtc))
            return Result<LessonChangeRequestResponse>.Failure(new("scheduling.request_already_pending", "Bu oluşum için zaten bekleyen bir talep var."));

        var request = new LessonChangeRequest(
            _idGenerator.New(),
            lesson.Id,
            lesson.StudentId,
            lesson.TeacherUserId.Value,
            command.Reason.Trim(),
            command.ProposedStartAtUtc,
            command.ProposedEndAtUtc,
            occurrenceStartAtUtc,
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
    private static readonly Error OccurrenceUnknown = new(
        "scheduling.request_occurrence_unknown",
        "Bu talep hangi haftanın dersi için açıldığını taşımıyor (eski talep); öğrenciden yeni talep isteyin.");
    private readonly ILessonChangeRequestRepository _repository;
    private readonly ILessonScheduleRepository _lessonRepository;
    private readonly ICommandDispatcher _dispatcher;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;
    private readonly ITeacherScheduleLock _scheduleLock;

    public AcceptLessonChangeRequestCommandHandler(
        ILessonChangeRequestRepository repository,
        ILessonScheduleRepository lessonRepository,
        ICommandDispatcher dispatcher,
        IClock clock,
        ITeacherScheduleLock scheduleLock,
        ILocalCalendar calendar)
    {
        _calendar = calendar;
        _scheduleLock = scheduleLock;
        _repository = repository;
        _lessonRepository = lessonRepository;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<Result<LessonChangeRequestResponse>> Handle(AcceptLessonChangeRequestCommand command, CancellationToken cancellationToken)
    {
        var initial = await _repository.GetByIdAsync(command.RequestId, cancellationToken);
        if (initial is null) return Result<LessonChangeRequestResponse>.Failure(NotFound);
        var teacher = initial.TeacherUserId;
        return await _scheduleLock.RunExclusiveAsync(teacher, token => HandleLocked(command, token), cancellationToken);
    }

    private async Task<Result<LessonChangeRequestResponse>> HandleLocked(AcceptLessonChangeRequestCommand command, CancellationToken cancellationToken)
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

        var source = await _lessonRepository.GetByIdAsync(request.LessonScheduleId, cancellationToken);
        if (source is null) return Result<LessonChangeRequestResponse>.Failure(new("scheduling.lesson_not_found", "Ders planı bulunamadı."));
        if (request.OccurrenceStartAtUtc is not { } key) return Result<LessonChangeRequestResponse>.Failure(OccurrenceUnknown);
        var activeError = await ChangeRequestOccurrenceRules.ValidateAsync(_lessonRepository, source, key, _calendar, _clock.UtcNow, cancellationToken);
        if (activeError is not null) return Result<LessonChangeRequestResponse>.Failure(activeError);
        if (request.ProposedStartAtUtc <= _clock.UtcNow)
            return Result<LessonChangeRequestResponse>.Failure(new("scheduling.invalid_range", "Önerilen başlangıç gelecekte olmalıdır."));

        // Alternatif tarih önerildiyse: dersi taşımak için MEVCUT Reschedule akışını yeniden kullan (DRY, çakışma/edit kontrolü dahil).
        // Erteleme başarısızsa talep kabul edilmez; hata olduğu gibi döndürülür.
        if (request.ProposedStartAtUtc is { } start && request.ProposedEndAtUtc is { } end)
        {
            // P17 T1 / N-01: tekrarlı derste yalnız talebin oluşumu taşınır (Single + orijinal anahtar) — eskiden kapsamsız
            // erteleme bütün seriyi kaydırıyordu. Anahtarı göçte kurtarılamamış eski talep seriyi kaydırmaz, açıkça reddedilir.
            var lesson = await _lessonRepository.GetByIdAsync(request.LessonScheduleId, cancellationToken);
            var isRecurring = lesson is not null && !string.IsNullOrWhiteSpace(lesson.RecurrenceRule);
            if (isRecurring && request.OccurrenceStartAtUtc is null)
            {
                return Result<LessonChangeRequestResponse>.Failure(OccurrenceUnknown);
            }

            var reschedule = await _dispatcher.Dispatch(
                isRecurring
                    ? new RescheduleLessonScheduleCommand(
                        request.LessonScheduleId, start, end, request.Reason, OccurrenceScope.Single, request.OccurrenceStartAtUtc)
                    : new RescheduleLessonScheduleCommand(request.LessonScheduleId, start, end, request.Reason),
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
            request.OccurrenceStartAtUtc,
            request.Status.ToString(),
            request.CreatedOnUtc,
            request.ResolvedOnUtc);
    }
}

internal static class ChangeRequestOccurrenceRules
{
    public static async Task<Error?> ValidateAsync(ILessonScheduleRepository repository, LessonSchedule lesson, DateTime key, ILocalCalendar calendar, DateTime now, CancellationToken ct)
    {
        if (!OccurrenceScopeRules.IsOccurrence(lesson, key, calendar)) return OccurrenceScopeRules.OccurrenceNotFound;
        if (!lesson.IsEditable) return new("scheduling.occurrence_not_active", "Yalnızca gelecek ve aktif bir ders için talep açılabilir.");
        var exceptions = await repository.ListExceptionsForSeriesAsync(lesson.Id, ct);
        var occurrence = exceptions.SingleOrDefault(x => x.OriginalStartAtUtc == key);
        if (occurrence?.Action is OccurrenceExceptionAction.Cancelled or OccurrenceExceptionAction.Skipped or OccurrenceExceptionAction.Completed
            || (occurrence?.OverrideStartAtUtc ?? key) <= now)
            return new("scheduling.occurrence_not_active", "Yalnızca gelecek ve aktif bir ders için talep açılabilir.");
        return null;
    }
}
