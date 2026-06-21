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
    Guid PaymentRecordId,
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
    string? Notes);

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
    IReadOnlyCollection<PaymentCurrencySummaryResponse> CurrencySummaries);

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

    public CreatePaymentRecordCommandHandler(IPaymentRecordRepository repository, IIdGenerator idGenerator, IClock clock)
    {
        _repository = repository;
        _idGenerator = idGenerator;
        _clock = clock;
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
            command.Status,
            command.BillingPeriodStartUtc,
            command.BillingPeriodEndUtc,
            command.Notes?.Trim(),
            _clock.UtcNow);

        await _repository.AddAsync(paymentRecord, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<PaymentRecordResponse>.Success(paymentRecord.ToResponse(_clock.UtcNow));
    }
}

public sealed class UpdatePaymentRecordCommandHandler : ICommandHandler<UpdatePaymentRecordCommand, Result<PaymentRecordResponse>>
{
    private static readonly Error NotFound = new("payments.record_not_found", "Odeme kaydi bulunamadi.");
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;

    public UpdatePaymentRecordCommandHandler(IPaymentRecordRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<PaymentRecordResponse>> Handle(UpdatePaymentRecordCommand command, CancellationToken cancellationToken)
    {
        var paymentRecord = await _repository.GetByIdAsync(command.PaymentRecordId, cancellationToken);
        if (paymentRecord is null)
        {
            return Result<PaymentRecordResponse>.Failure(NotFound);
        }

        paymentRecord.UpdateManualTracking(
            command.RelatedLessonSessionId,
            command.ItemType,
            command.Description.Trim(),
            command.Currency.Trim().ToUpperInvariant(),
            command.ExpectedAmount,
            command.CollectedAmount,
            command.DueDateUtc,
            command.CollectedOnUtc,
            command.Status,
            command.BillingPeriodStartUtc,
            command.BillingPeriodEndUtc,
            command.Notes?.Trim(),
            _clock.UtcNow);

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<PaymentRecordResponse>.Success(paymentRecord.ToResponse(_clock.UtcNow));
    }
}

public sealed class GetPaymentRecordByIdQueryHandler : IQueryHandler<GetPaymentRecordByIdQuery, Result<PaymentRecordResponse>>
{
    private static readonly Error NotFound = new("payments.record_not_found", "Odeme kaydi bulunamadi.");
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;

    public GetPaymentRecordByIdQueryHandler(IPaymentRecordRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<PaymentRecordResponse>> Handle(GetPaymentRecordByIdQuery query, CancellationToken cancellationToken)
    {
        var paymentRecord = await _repository.GetByIdAsync(query.PaymentRecordId, cancellationToken);
        return paymentRecord is null
            ? Result<PaymentRecordResponse>.Failure(NotFound)
            : Result<PaymentRecordResponse>.Success(paymentRecord.ToResponse(_clock.UtcNow));
    }
}

public sealed class ListPaymentRecordsForTeacherQueryHandler : IQueryHandler<ListPaymentRecordsForTeacherQuery, Result<IReadOnlyCollection<PaymentRecordResponse>>>
{
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;

    public ListPaymentRecordsForTeacherQueryHandler(IPaymentRecordRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyCollection<PaymentRecordResponse>>> Handle(ListPaymentRecordsForTeacherQuery query, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var paymentRecords = await _repository.ListByTeacherUserIdAsync(query.TeacherUserId, cancellationToken);
        var payload = paymentRecords
            .Where(record => !query.OutstandingOnly || record.IsOutstanding(now))
            .OrderBy(record => record.DueDateUtc)
            .ThenBy(record => record.Description)
            .Select(record => record.ToResponse(now))
            .ToArray();

        return Result<IReadOnlyCollection<PaymentRecordResponse>>.Success(payload);
    }
}

public sealed class GetTeacherPaymentSummaryQueryHandler : IQueryHandler<GetTeacherPaymentSummaryQuery, Result<TeacherPaymentSummaryResponse>>
{
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;

    public GetTeacherPaymentSummaryQueryHandler(IPaymentRecordRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
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
                    records.Count(record => record.Status == PaymentStatus.Pending && !record.IsOverdue(now)),
                    records.Count(record => record.Status == PaymentStatus.PartiallyPaid && !record.IsOverdue(now)),
                    records.Count(record => record.Status == PaymentStatus.Paid),
                    records.Count(record => record.IsOverdue(now)),
                    records.Count(record => record.Status == PaymentStatus.Cancelled),
                    records.Sum(record => record.ExpectedAmount),
                    records.Sum(record => record.CollectedAmount),
                    records.Sum(record => record.GetOutstandingAmount()),
                    records.Where(record => record.IsOverdue(now)).Sum(record => record.GetOutstandingAmount()));
            })
            .ToArray();

        return Result<TeacherPaymentSummaryResponse>.Success(new TeacherPaymentSummaryResponse(
            query.TeacherUserId,
            paymentRecords.Count,
            currencySummaries));
    }
}

public sealed class ListFilteredPaymentRecordsForTeacherQueryHandler : IQueryHandler<ListFilteredPaymentRecordsForTeacherQuery, Result<IReadOnlyCollection<PaymentRecordResponse>>>
{
    private readonly IPaymentRecordRepository _repository;
    private readonly IClock _clock;

    public ListFilteredPaymentRecordsForTeacherQueryHandler(IPaymentRecordRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
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
            var isOverdue = record.IsOverdue(now);
            var isPaid = record.Status == PaymentStatus.Paid;

            return (query.Outstanding && isOutstanding)
                || (query.Overdue && isOverdue)
                || (query.Paid && isPaid);
        });

        var payload = filtered
            .OrderBy(x => x.DueDateUtc)
            .ThenBy(x => x.Description)
            .Select(x => x.ToResponse(now))
            .ToArray();

        return Result<IReadOnlyCollection<PaymentRecordResponse>>.Success(payload);
    }
}

internal static class PaymentRecordMappings
{
    public static PaymentRecordResponse ToResponse(this PaymentRecord paymentRecord, DateTime now)
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
            paymentRecord.GetDisplayStatus(now).ToString(),
            paymentRecord.IsOverdue(now),
            paymentRecord.BillingPeriodStartUtc,
            paymentRecord.BillingPeriodEndUtc,
            paymentRecord.Notes);
    }

    public static decimal GetOutstandingAmount(this PaymentRecord paymentRecord)
    {
        return Math.Max(paymentRecord.ExpectedAmount - paymentRecord.CollectedAmount, 0);
    }

    public static bool IsOutstanding(this PaymentRecord paymentRecord, DateTime now)
    {
        return paymentRecord.GetOutstandingAmount() > 0 && paymentRecord.Status != PaymentStatus.Cancelled;
    }

    public static bool IsOverdue(this PaymentRecord paymentRecord, DateTime now)
    {
        return paymentRecord.GetOutstandingAmount() > 0
            && paymentRecord.Status != PaymentStatus.Cancelled
            && paymentRecord.DueDateUtc < now;
    }

    public static PaymentStatus GetDisplayStatus(this PaymentRecord paymentRecord, DateTime now)
    {
        return paymentRecord.IsOverdue(now) ? PaymentStatus.Overdue : paymentRecord.Status;
    }
}
