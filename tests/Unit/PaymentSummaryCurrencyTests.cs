using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class PaymentSummaryCurrencyTests
{
    private static readonly DateTime Now = new(2026, 11, 30, 22, 0, 0, DateTimeKind.Utc); // 1 Aralık TR
    private static readonly Guid Teacher = Guid.NewGuid();

    [Fact]
    public async Task Monthly_points_never_add_try_to_usd_and_keep_six_local_months_per_currency()
    {
        var records = new[] { Record("TRY", 1000, 400), Record("USD", 80, 20), Record("TRY", 500, 0, PaymentStatus.Cancelled) };
        var result = await Handler(records).Handle(new(Teacher), default);
        var summary = result.Value!;
        Assert.Equal(2, summary.CurrencySummaries.Count);
        Assert.Equal(1000m, summary.CurrencySummaries.Single(x => x.Currency == "TRY").ExpectedAmountTotal);
        Assert.Equal(60m, summary.CurrencySummaries.Single(x => x.Currency == "USD").OutstandingAmountTotal);
        Assert.Equal(12, summary.MonthlyBreakdown.Count);
        var current = summary.MonthlyBreakdown.Where(x => x.Year == 2026 && x.Month == 12).ToArray();
        Assert.Equal(1000m, current.Single(x => x.Currency == "TRY").ExpectedAmount);
        Assert.Equal(400m, current.Single(x => x.Currency == "TRY").CollectedAmount);
        Assert.Equal(80m, current.Single(x => x.Currency == "USD").ExpectedAmount);
        Assert.Equal(20m, current.Single(x => x.Currency == "USD").CollectedAmount);
        Assert.Equal(0m, summary.MonthlyBreakdown.Single(x => x.Currency == "USD" && x.Month == 7).ExpectedAmount);
    }

    [Fact]
    public async Task No_records_does_not_invent_a_currency()
    {
        var result = await Handler([]).Handle(new(Teacher), default);
        Assert.Empty(result.Value!.CurrencySummaries);
        Assert.Empty(result.Value.MonthlyBreakdown);
    }

    private static GetTeacherPaymentSummaryQueryHandler Handler(IReadOnlyCollection<PaymentRecord> records)
        => new(new Repository(records), new Clock(), new LocalCalendar("Europe/Istanbul", new Clock()));
    private static PaymentRecord Record(string currency, decimal expected, decimal collected, PaymentStatus status = PaymentStatus.Pending)
        => new(Guid.NewGuid(), Teacher, Guid.NewGuid(), null, BillingItemType.LessonFee, "Ders", currency,
            expected, collected, Now, null, status, null, null, null, Now);
    private sealed class Clock : IClock { public DateTime UtcNow => Now; }
    private sealed class Repository(IReadOnlyCollection<PaymentRecord> records) : IPaymentRecordRepository
    {
        public Task<PaymentRecord?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(records.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyCollection<PaymentRecord>> ListByTeacherUserIdAsync(Guid teacher, CancellationToken ct) => Task.FromResult(records);
        public Task AddAsync(PaymentRecord payment, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
