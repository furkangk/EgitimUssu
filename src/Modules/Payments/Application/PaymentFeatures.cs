using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Payments.Application;

public sealed record CreatePaymentRecordCommand(
    Guid TeacherUserId,
    Guid StudentId,
    Guid? RelatedLessonSessionId,
    BillingItemType ItemType,
    string Description,
    string Currency,
    decimal ExpectedAmount,
    decimal CollectedAmount,
    DateTime DueDateUtc,
    DateTime? CollectedOnUtc,
    PaymentStatus Status,
    DateTime? BillingPeriodStartUtc,
    DateTime? BillingPeriodEndUtc,
    string? Notes) : ICommand<Result<PaymentRecordResponse>>;

public sealed record UpdatePaymentRecordCommand(
    Guid PaymentRecordId, string Description, decimal ExpectedAmount, DateTime DueDateUtc, string? Notes)
    : ICommand<Result<PaymentRecordResponse>>;

public sealed record AddCollectionCommand(Guid PaymentRecordId, decimal Amount, PaymentMethod Method, DateTime OccurredOnUtc, string? Note)
    : ICommand<Result<PaymentRecordResponse>>;
public sealed record RefundPaymentCommand(Guid PaymentRecordId, decimal Amount, PaymentMethod Method, DateTime OccurredOnUtc, string? Note)
    : ICommand<Result<PaymentRecordResponse>>;
public sealed record CancelPaymentCommand(Guid PaymentRecordId) : ICommand<Result<PaymentRecordResponse>>;

public sealed record GetPaymentRecordByIdQuery(Guid PaymentRecordId) : IQuery<Result<PaymentRecordResponse>>;

public sealed record ListPaymentRecordsForTeacherQuery(Guid TeacherUserId, bool OutstandingOnly) : IQuery<Result<IReadOnlyCollection<PaymentRecordResponse>>>;
public sealed record ListFilteredPaymentRecordsForTeacherQuery(
    Guid TeacherUserId,
    bool Outstanding,
    bool Overdue,
    bool Paid,
    DateTime? DateFromUtc,
    DateTime? DateToUtc) : IQuery<Result<IReadOnlyCollection<PaymentRecordResponse>>>;

public sealed record GetTeacherPaymentSummaryQuery(Guid TeacherUserId) : IQuery<Result<TeacherPaymentSummaryResponse>>;

/// <summary>
/// Öğretmenin ödemelerinde sunucu tarafı arama + filtre + sayfalama.
/// <paramref name="Status"/>: Paid/Pending/PartiallyPaid/Overdue/Cancelled (görüntüleme statüsü; Overdue hesaplanır).
/// </summary>
public sealed record SearchPaymentRecordsForTeacherQuery(
    Guid TeacherUserId,
    string? Query,
    string? Status,
    Guid? StudentId,
    DateTime? DateFromUtc,
    DateTime? DateToUtc,
    int Skip,
    int Take) : IQuery<Result<PagedPaymentRecordsResponse>>;

public sealed record PaymentRecordResponse(
    Guid Id,
    Guid TeacherUserId,
    Guid StudentId,
    Guid? RelatedLessonSessionId,
    string ItemType,
    string Description,
    string Currency,
    decimal ExpectedAmount,
    decimal CollectedAmount,
    decimal OutstandingAmount,
    DateTime DueDateUtc,
    DateTime? CollectedOnUtc,
    string Status,
    bool IsOverdue,
    DateTime? BillingPeriodStartUtc,
    DateTime? BillingPeriodEndUtc,
    string? Notes,
    IReadOnlyCollection<PaymentTransactionResponse> Transactions);

public sealed record PaymentTransactionResponse(Guid Id, string Kind, decimal Amount, string Method, DateTime OccurredOnUtc, string? Note);

public sealed record PaymentCurrencySummaryResponse(
    string Currency,
    int PendingCount,
    int PartialCount,
    int PaidCount,
    int OverdueCount,
    int CancelledCount,
    decimal ExpectedAmountTotal,
    decimal CollectedAmountTotal,
    decimal OutstandingAmountTotal,
    decimal OverdueAmountTotal);

public sealed record TeacherPaymentSummaryResponse(
    Guid TeacherUserId,
    int TotalRecords,
    IReadOnlyCollection<PaymentCurrencySummaryResponse> CurrencySummaries,
    IReadOnlyCollection<PaymentMonthlyPointResponse> MonthlyBreakdown);

/// <summary>Son aylara ait beklenen/tahsil edilen toplamı (grafikler için; iptaller hariç).</summary>
public sealed record PaymentMonthlyPointResponse(
    int Year,
    int Month,
    decimal ExpectedAmount,
    decimal CollectedAmount,
    string Currency);

/// <summary>Sayfalı ödeme listesi + toplam eşleşen kayıt sayısı.</summary>
public sealed record PagedPaymentRecordsResponse(
    IReadOnlyCollection<PaymentRecordResponse> Items,
    int TotalCount);

public interface IPaymentRecordRepository
{
    Task<PaymentRecord?> GetByIdAsync(Guid paymentRecordId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PaymentRecord>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken);

    Task AddAsync(PaymentRecord paymentRecord, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class CreatePaymentRecordCommandHandler : ICommandHandler<CreatePaymentRecordCommand, Result<PaymentRecordResponse>>
{
    private readonly IPaymentRecordRepository _repository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;

    public CreatePaymentRecordCommandHandler(IPaymentRecordRepository repository, IIdGenerator idGenerator, IClock clock, ILocalCalendar calendar)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
        _calendar = calendar;
    }

    public async Task<Result<PaymentRecordResponse>> Handle(CreatePaymentRecordCommand command, CancellationToken cancellationToken)
    {
        var paymentRecord = new PaymentRecord(
            _idGenerator.New(),
            command.TeacherUserId,
            command.StudentId,
            command.RelatedLessonSessionId,
            command.ItemType,
            command.Description.Trim(),
            command.Currency.Trim().ToUpperInvariant(),
            command.ExpectedAmount,
            command.CollectedAmount,
            command.DueDateUtc,
            command.CollectedOnUtc,
            PaymentStatus.Pending,
            command.BillingPeriodStartUtc,
            command.BillingPeriodEndUtc,
            command.Notes?.Trim(),
            _clock.UtcNow);

        await _repository.AddAsync(paymentRecord, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<PaymentRecordResponse>.Success(paymentRecord.ToResponse(_calendar));
    }
}

public sealed class UpdatePaymentRecordCommandHandler : ICommandHandler<UpdatePaymentRecordCommand, Result<PaymentRecordResponse>>
{
    private static readonly Error NotFound = new("payments.record_not_found", "Odeme kaydi bulunamadi.");
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;
    private readonly IRequestEntityCache _entityCache;

    public UpdatePaymentRecordCommandHandler(IPaymentRecordRepository repository, IClock clock, IRequestEntityCache entityCache, ILocalCalendar calendar)
    {
        _repository = repository;
        _clock = clock;
        _calendar = calendar;
        _entityCache = entityCache;
    }

    /// <summary>O2: Authorizer kaydı yüklediyse istek kapsamından okur; ikinci sorgu yapılmaz.</summary>
    public async Task<Result<PaymentRecordResponse>> Handle(UpdatePaymentRecordCommand command, CancellationToken cancellationToken)
    {
        var paymentRecord = await _entityCache.LoadPaymentRecordAsync(_repository, command.PaymentRecordId, cancellationToken);
        if (paymentRecord is null)
        {
            return Result<PaymentRecordResponse>.Failure(NotFound);
        }

        var update = paymentRecord.UpdateDetails(command.Description, command.ExpectedAmount,
            command.DueDateUtc, command.Notes, _clock.UtcNow);
        if (update.IsFailure) return Result<PaymentRecordResponse>.Failure(update.Error);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<PaymentRecordResponse>.Success(paymentRecord.ToResponse(_calendar));
    }
}

public sealed class GetPaymentRecordByIdQueryHandler : IQueryHandler<GetPaymentRecordByIdQuery, Result<PaymentRecordResponse>>
{
    private static readonly Error NotFound = new("payments.record_not_found", "Odeme kaydi bulunamadi.");
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;
    private readonly IRequestEntityCache _entityCache;

    public GetPaymentRecordByIdQueryHandler(IPaymentRecordRepository repository, IClock clock, IRequestEntityCache entityCache, ILocalCalendar calendar)
    {
        _repository = repository;
        _clock = clock;
        _calendar = calendar;
        _entityCache = entityCache;
    }

    /// <summary>O2: Authorizer kaydı yüklediyse istek kapsamından okur; ikinci sorgu yapılmaz.</summary>
    public async Task<Result<PaymentRecordResponse>> Handle(GetPaymentRecordByIdQuery query, CancellationToken cancellationToken)
    {
        var paymentRecord = await _entityCache.LoadPaymentRecordAsync(_repository, query.PaymentRecordId, cancellationToken);
        return paymentRecord is null
            ? Result<PaymentRecordResponse>.Failure(NotFound)
            : Result<PaymentRecordResponse>.Success(paymentRecord.ToResponse(_calendar));
    }
}

public sealed class ListPaymentRecordsForTeacherQueryHandler : IQueryHandler<ListPaymentRecordsForTeacherQuery, Result<IReadOnlyCollection<PaymentRecordResponse>>>
{
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;

    public ListPaymentRecordsForTeacherQueryHandler(IPaymentRecordRepository repository, IClock clock, ILocalCalendar calendar)
    {
        _repository = repository;
        _clock = clock;
        _calendar = calendar;
    }

    public async Task<Result<IReadOnlyCollection<PaymentRecordResponse>>> Handle(ListPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var paymentRecords = await _repository.ListByTeacherUserIdAsync(query.TeacherUserId, cancellationToken);
        var payload = paymentRecords
            .Where(record => !query.OutstandingOnly || record.IsOutstanding(now))
            .OrderBy(record => record.DueDateUtc)
            .ThenBy(record => record.Description)
            .Select(record => record.ToResponse(_calendar))
            .ToArray();

        return Result<IReadOnlyCollection<PaymentRecordResponse>>.Success(payload);
    }
}

public sealed class GetTeacherPaymentSummaryQueryHandler : IQueryHandler<GetTeacherPaymentSummaryQuery, Result<TeacherPaymentSummaryResponse>>
{
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;


    public GetTeacherPaymentSummaryQueryHandler(IPaymentRecordRepository repository, IClock clock, ILocalCalendar calendar)
    {
        _repository = repository;
        _clock = clock;
        _calendar = calendar;
    }

    public async Task<Result<TeacherPaymentSummaryResponse>> Handle(GetTeacherPaymentSummaryQuery query, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var paymentRecords = await _repository.ListByTeacherUserIdAsync(query.TeacherUserId, cancellationToken);
        var currencySummaries = paymentRecords
            .GroupBy(record => record.Currency)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var records = group.ToArray();
                return new PaymentCurrencySummaryResponse(
                    group.Key,
                    records.Count(record => record.Status == PaymentStatus.Pending && !record.IsOverdue(_calendar)),
                    records.Count(record => record.Status == PaymentStatus.PartiallyPaid && !record.IsOverdue(_calendar)),
                    records.Count(record => record.Status == PaymentStatus.Paid),
                    records.Count(record => record.IsOverdue(_calendar)),
                    records.Count(record => record.Status == PaymentStatus.Cancelled),
                    records.Where(record => record.Status != PaymentStatus.Cancelled).Sum(record => record.ExpectedAmount),
                    records.Sum(record => record.CollectedAmount),
                    records.Sum(record => record.GetOutstandingAmount()),
                    records.Where(record => record.IsOverdue(_calendar)).Sum(record => record.GetOutstandingAmount()));
            })
            .ToArray();

        var monthlyBreakdown = BuildMonthlyBreakdown(paymentRecords, _calendar);

        return Result<TeacherPaymentSummaryResponse>.Success(new TeacherPaymentSummaryResponse(
            query.TeacherUserId,
            paymentRecords.Count,
            currencySummaries,
            monthlyBreakdown));
    }

    // Son 6 ayın (eskiden yeniye) beklenen/tahsil toplamı — vade ayına göre; iptaller hariç.
    // P16 T5 / N-11 (K-19): ay Türkiye ayıdır — "bu ay" ve kaydın vade ayı yerel tarihten (ayın 1'i 01:00 TR vadeli
    // kayıt önceki aya düşmez). Her para birimi ayrı seri olarak döner.
    private static IReadOnlyCollection<PaymentMonthlyPointResponse> BuildMonthlyBreakdown(
        IReadOnlyCollection<PaymentRecord> records,
        ILocalCalendar calendar)
    {
        var active = records
            .Where(record => record.Status != PaymentStatus.Cancelled)
            .Select(record => (Record: record, DueDate: calendar.ToLocalDate(record.DueDateUtc)))
            .ToArray();
        var today = calendar.Today();
        var firstMonth = new DateOnly(today.Year, today.Month, 1);
        var currencies = records.Select(record => record.Currency).Distinct().OrderBy(currency => currency).ToArray();
        var points = new List<PaymentMonthlyPointResponse>(6 * currencies.Length);
        for (var offset = 5; offset >= 0; offset--)
        {
            var month = firstMonth.AddMonths(-offset);
            foreach (var currency in currencies)
            {
                var inMonth = active
                    .Where(item => item.Record.Currency == currency && item.DueDate.Year == month.Year && item.DueDate.Month == month.Month)
                    .Select(item => item.Record)
                    .ToArray();
                points.Add(new PaymentMonthlyPointResponse(
                    month.Year, month.Month,
                    inMonth.Sum(record => record.ExpectedAmount),
                    inMonth.Sum(record => record.CollectedAmount), currency));
            }
        }

        return points;
    }
}

public sealed class SearchPaymentRecordsForTeacherQueryHandler : IQueryHandler<SearchPaymentRecordsForTeacherQuery, Result<PagedPaymentRecordsResponse>>
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;

    public SearchPaymentRecordsForTeacherQueryHandler(IPaymentRecordRepository repository, IClock clock, ILocalCalendar calendar)
    {
        _repository = repository;
        _clock = clock;
        _calendar = calendar;
    }

    public async Task<Result<PagedPaymentRecordsResponse>> Handle(SearchPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var records = await _repository.ListByTeacherUserIdAsync(query.TeacherUserId, cancellationToken);
        var text = query.Query?.Trim();

        var filtered = records
            .Where(record =>
            {
                if (!string.IsNullOrEmpty(text)
                    && !record.Description.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (query.StudentId.HasValue && record.StudentId != query.StudentId.Value)
                {
                    return false;
                }

                if (query.DateFromUtc.HasValue && record.DueDateUtc < query.DateFromUtc.Value)
                {
                    return false;
                }

                if (query.DateToUtc.HasValue && record.DueDateUtc > query.DateToUtc.Value)
                {
                    return false;
                }

                return string.IsNullOrEmpty(query.Status) || MatchesStatus(record, query.Status, now, _calendar);
            })
            .OrderBy(record => record.DueDateUtc)
            .ThenBy(record => record.Description)
            .ToArray();

        var take = query.Take <= 0 ? DefaultTake : Math.Min(query.Take, MaxTake);
        var skip = Math.Max(query.Skip, 0);
        var items = filtered
            .Skip(skip)
            .Take(take)
            .Select(record => record.ToResponse(_calendar))
            .ToArray();

        return Result<PagedPaymentRecordsResponse>.Success(
            new PagedPaymentRecordsResponse(items, filtered.Length));
    }

    private static bool MatchesStatus(PaymentRecord record, string status, DateTime now, ILocalCalendar calendar) => status switch
    {
        // Açık (gecikmemiş bakiyeli): Pending + PartiallyPaid — "Bekleyen" sekmesi.
        "Open" => record.IsOutstanding(now) && !record.IsOverdue(calendar),
        "Overdue" => record.IsOverdue(calendar),
        "Pending" => record.Status == PaymentStatus.Pending && !record.IsOverdue(calendar),
        "PartiallyPaid" => record.Status == PaymentStatus.PartiallyPaid && !record.IsOverdue(calendar),
        "Paid" => record.Status == PaymentStatus.Paid,
        "Cancelled" => record.Status == PaymentStatus.Cancelled,
        _ => true,
    };
}

public sealed class ListFilteredPaymentRecordsForTeacherQueryHandler : IQueryHandler<ListFilteredPaymentRecordsForTeacherQuery, Result<IReadOnlyCollection<PaymentRecordResponse>>>
{
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;
    private readonly ILocalCalendar _calendar;

    public ListFilteredPaymentRecordsForTeacherQueryHandler(IPaymentRecordRepository repository, IClock clock, ILocalCalendar calendar)
    {
        _repository = repository;
        _clock = clock;
        _calendar = calendar;
    }

    public async Task<Result<IReadOnlyCollection<PaymentRecordResponse>>> Handle(ListFilteredPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var records = await _repository.ListByTeacherUserIdAsync(query.TeacherUserId, cancellationToken);

        var filtered = records.Where(record =>
        {
            if (query.DateFromUtc.HasValue && record.DueDateUtc < query.DateFromUtc.Value)
            {
                return false;
            }

            if (query.DateToUtc.HasValue && record.DueDateUtc > query.DateToUtc.Value)
            {
                return false;
            }

            if (!query.Outstanding && !query.Overdue && !query.Paid)
            {
                return true;
            }

            var isOutstanding = record.IsOutstanding(now);
            var isOverdue = record.IsOverdue(_calendar);
            var isPaid = record.Status == PaymentStatus.Paid;

            return (query.Outstanding && isOutstanding)
                || (query.Overdue && isOverdue)
                || (query.Paid && isPaid);
        });

        var payload = filtered
            .OrderBy(x => x.DueDateUtc)
            .ThenBy(x => x.Description)
            .Select(x => x.ToResponse(_calendar))
            .ToArray();

        return Result<IReadOnlyCollection<PaymentRecordResponse>>.Success(payload);
    }
}

internal static class PaymentRecordMappings
{
    public static PaymentRecordResponse ToResponse(this PaymentRecord paymentRecord, ILocalCalendar calendar)
    {
        return new PaymentRecordResponse(
            paymentRecord.Id,
            paymentRecord.TeacherUserId,
            paymentRecord.StudentId,
            paymentRecord.RelatedLessonSessionId,
            paymentRecord.ItemType.ToString(),
            paymentRecord.Description,
            paymentRecord.Currency,
            paymentRecord.ExpectedAmount,
            paymentRecord.CollectedAmount,
            paymentRecord.GetOutstandingAmount(),
            paymentRecord.DueDateUtc,
            paymentRecord.CollectedOnUtc,
            paymentRecord.GetDisplayStatus(calendar).ToString(),
            paymentRecord.IsOverdue(calendar),
            paymentRecord.BillingPeriodStartUtc,
            paymentRecord.BillingPeriodEndUtc,
            paymentRecord.Notes,
            paymentRecord.Transactions.OrderBy(x => x.OccurredOnUtc).ThenBy(x => x.Id)
                .Select(x => new PaymentTransactionResponse(x.Id, x.Kind.ToString(), x.Amount, x.Method.ToString(), x.OccurredOnUtc, x.Note)).ToArray());
    }

    public static decimal GetOutstandingAmount(this PaymentRecord paymentRecord)
    {
        // İptal edilmiş ödeme borç doğurmaz; tahsil edilmemiş tutarı bakiyeye katma.
        if (paymentRecord.Status == PaymentStatus.Cancelled)
        {
            return 0;
        }

        return Math.Max(paymentRecord.ExpectedAmount - paymentRecord.CollectedAmount, 0);
    }

    public static bool IsOutstanding(this PaymentRecord paymentRecord, DateTime now)
    {
        return paymentRecord.GetOutstandingAmount() > 0;
    }

    public static bool IsOverdue(this PaymentRecord paymentRecord, ILocalCalendar calendar)
    {
        return paymentRecord.GetOutstandingAmount() > 0
            && calendar.ToLocalDate(paymentRecord.DueDateUtc) < calendar.Today();
    }

    public static PaymentStatus GetDisplayStatus(this PaymentRecord paymentRecord, ILocalCalendar calendar)
    {
        return paymentRecord.Status == PaymentStatus.Cancelled ? PaymentStatus.Cancelled
            : paymentRecord.CollectedAmount >= paymentRecord.ExpectedAmount ? PaymentStatus.Paid
            : paymentRecord.CollectedAmount > 0m ? PaymentStatus.PartiallyPaid
            : paymentRecord.IsOverdue(calendar) ? PaymentStatus.Overdue : PaymentStatus.Pending;
    }
}

public sealed class PaymentTransactionCommandHandler(IPaymentRecordRepository repository, IClock clock, IIdGenerator ids, IRequestEntityCache cache, ILocalCalendar calendar)
    : ICommandHandler<AddCollectionCommand, Result<PaymentRecordResponse>>,
      ICommandHandler<RefundPaymentCommand, Result<PaymentRecordResponse>>,
      ICommandHandler<CancelPaymentCommand, Result<PaymentRecordResponse>>
{
    public Task<Result<PaymentRecordResponse>> Handle(AddCollectionCommand command, CancellationToken ct)
        => Apply(command.PaymentRecordId, p => p.AddCollection(ids.New(), command.Amount, command.Method, command.OccurredOnUtc, command.Note, clock.UtcNow), ct);
    public Task<Result<PaymentRecordResponse>> Handle(RefundPaymentCommand command, CancellationToken ct)
        => Apply(command.PaymentRecordId, p => p.Refund(ids.New(), command.Amount, command.Method, command.OccurredOnUtc, command.Note, clock.UtcNow), ct);
    public Task<Result<PaymentRecordResponse>> Handle(CancelPaymentCommand command, CancellationToken ct)
        => Apply(command.PaymentRecordId, p => p.Cancel(clock.UtcNow), ct);
    private async Task<Result<PaymentRecordResponse>> Apply(Guid id, Func<PaymentRecord, Result> action, CancellationToken ct)
    {
        var record = await cache.LoadPaymentRecordAsync(repository, id, ct);
        if (record is null) return Result<PaymentRecordResponse>.Failure(new Error("payments.record_not_found", "Ödeme kaydı bulunamadı."));
        var result = action(record);
        if (result.IsFailure) return Result<PaymentRecordResponse>.Failure(result.Error);
        await repository.SaveChangesAsync(ct);
        return Result<PaymentRecordResponse>.Success(record.ToResponse(calendar));
    }
}
