using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// Denetim bug avı (2026-07-06): Öğretmen ödeme özetinde <see cref="PaymentStatus.Cancelled"/>
/// kayıtların tahsil edilmemiş tutarı "ödenmemiş bakiye" (OutstandingAmountTotal) toplamına
/// sızmamalı. İptal edilmiş bir ödeme kimseye borç doğurmaz; bu davranış
/// <c>IsOutstanding()</c> (Status != Cancelled) predikatıyla tutarlı olmalıdır.
/// </summary>
public sealed class PaymentSummaryOutstandingTests
{
    private static readonly DateTime Now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.NewGuid();

    [Fact]
    public async Task Summary_Excludes_Cancelled_Records_From_OutstandingAmountTotal()
    {
        var records = new[]
        {
            // Ödenmiş: bakiye yok.
            CreateRecord(expected: 100m, collected: 100m, PaymentStatus.Paid, dueDaysFromNow: -5),
            // Açık (gelecekte vadesi): 200 bakiye katkısı.
            CreateRecord(expected: 300m, collected: 100m, PaymentStatus.Pending, dueDaysFromNow: 5),
            // İptal: expected-collected = 200 ama bakiyeye GİRMEMELİ.
            CreateRecord(expected: 200m, collected: 0m, PaymentStatus.Cancelled, dueDaysFromNow: -3),
        };

        var handler = new GetTeacherPaymentSummaryQueryHandler(
            new FakePaymentRecordRepository(records),
            new FixedClock(Now),
            new LocalCalendar("Europe/Istanbul", new FixedClock(Now)));

        var result = await handler.Handle(new GetTeacherPaymentSummaryQuery(TeacherUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var currency = Assert.Single(result.Value!.CurrencySummaries);
        Assert.Equal(1, currency.CancelledCount);
        // Yalnız açık kayıt (200) — iptal edilenin 200'ü sayılmamalı.
        Assert.Equal(200m, currency.OutstandingAmountTotal);
    }

    [Fact]
    public async Task Summary_Excludes_Cancelled_Records_From_OverdueAmountTotal()
    {
        var records = new[]
        {
            // Vadesi geçmiş açık kayıt: 150 gecikmiş.
            CreateRecord(expected: 150m, collected: 0m, PaymentStatus.Pending, dueDaysFromNow: -10),
            // İptal + vadesi geçmiş: gecikmiş toplama GİRMEMELİ.
            CreateRecord(expected: 500m, collected: 0m, PaymentStatus.Cancelled, dueDaysFromNow: -10),
        };

        var handler = new GetTeacherPaymentSummaryQueryHandler(
            new FakePaymentRecordRepository(records),
            new FixedClock(Now),
            new LocalCalendar("Europe/Istanbul", new FixedClock(Now)));

        var result = await handler.Handle(new GetTeacherPaymentSummaryQuery(TeacherUserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var currency = Assert.Single(result.Value!.CurrencySummaries);
        Assert.Equal(150m, currency.OverdueAmountTotal);
    }

    [Fact]
    public async Task Monthly_breakdown_uses_turkey_months()
    {
        // P16 T5 / N-11: "şimdi" 1 Aralık 01:00 TR (30 Kasım 22:00Z) → son nokta Aralık. 1 Aralık 01:00 TR vadeli kayıt
        // Aralık'ta, 30 Kasım 23:00 TR vadeli kayıt Kasım'da (eskiden ikisi de UTC'de Kasım'a düşüyordu).
        var now = new DateTime(2026, 11, 30, 22, 0, 0, DateTimeKind.Utc);
        var records = new[]
        {
            CreateRecordDueAt(100m, now),
            CreateRecordDueAt(40m, new DateTime(2026, 11, 30, 20, 0, 0, DateTimeKind.Utc)),
        };
        var handler = new GetTeacherPaymentSummaryQueryHandler(
            new FakePaymentRecordRepository(records),
            new FixedClock(now),
            new LocalCalendar("Europe/Istanbul", new FixedClock(now)));

        var result = await handler.Handle(new GetTeacherPaymentSummaryQuery(TeacherUserId), CancellationToken.None);

        var points = result.Value!.MonthlyBreakdown.ToArray();
        Assert.Equal((2026, 12), (points[^1].Year, points[^1].Month));
        Assert.Equal(100m, points[^1].ExpectedAmount);
        Assert.Equal((2026, 11), (points[^2].Year, points[^2].Month));
        Assert.Equal(40m, points[^2].ExpectedAmount);
        Assert.Equal((2026, 7), (points[0].Year, points[0].Month));   // altı ay: Temmuz → Aralık
    }

    private static PaymentRecord CreateRecordDueAt(decimal expected, DateTime dueDateUtc)
        => new(
            Guid.NewGuid(),
            TeacherUserId,
            studentId: Guid.NewGuid(),
            relatedLessonSessionId: null,
            BillingItemType.LessonFee,
            description: "Test",
            currency: "TRY",
            expectedAmount: expected,
            collectedAmount: 0m,
            dueDateUtc: dueDateUtc,
            collectedOnUtc: null,
            status: PaymentStatus.Pending,
            billingPeriodStartUtc: null,
            billingPeriodEndUtc: null,
            notes: null,
            createdOnUtc: dueDateUtc.AddDays(-30));

    private static PaymentRecord CreateRecord(decimal expected, decimal collected, PaymentStatus status, int dueDaysFromNow)
    {
        return new PaymentRecord(
            Guid.NewGuid(),
            TeacherUserId,
            studentId: Guid.NewGuid(),
            relatedLessonSessionId: null,
            BillingItemType.LessonFee,
            description: "Test",
            currency: "TRY",
            expectedAmount: expected,
            collectedAmount: collected,
            dueDateUtc: Now.AddDays(dueDaysFromNow),
            collectedOnUtc: status == PaymentStatus.Paid ? Now.AddDays(dueDaysFromNow) : null,
            status: status,
            billingPeriodStartUtc: null,
            billingPeriodEndUtc: null,
            notes: null,
            createdOnUtc: Now.AddDays(-30));
    }

    private sealed class FakePaymentRecordRepository(IReadOnlyCollection<PaymentRecord> records) : IPaymentRecordRepository
    {
        public Task<PaymentRecord?> GetByIdAsync(Guid paymentRecordId, CancellationToken cancellationToken)
            => Task.FromResult<PaymentRecord?>(null);

        public Task<IReadOnlyCollection<PaymentRecord>> ListByTeacherUserIdAsync(Guid teacherUserId, CancellationToken cancellationToken)
            => Task.FromResult(records);

        public Task AddAsync(PaymentRecord paymentRecord, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
