using EgitimUssu.Modules.Payments.Application;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// Sunucu tarafı arama + filtre + sayfalama (<see cref="SearchPaymentRecordsForTeacherQueryHandler"/>):
/// toplam sayı filtreli kümeden gelir, sayfa yalnız istenen dilimi döndürür; metin/durum filtreleri uygulanır.
/// </summary>
public sealed class PaymentSearchTests
{
    private static readonly DateTime Now = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.NewGuid();

    [Fact]
    public async Task Search_Paginates_And_Reports_Total()
    {
        var records = Enumerable.Range(0, 25)
            .Select(i => Record($"Ders {i:00}", PaymentStatus.Pending, dueDaysFromNow: i))
            .ToArray();
        var handler = NewHandler(records);

        var page = await handler.Handle(
            new SearchPaymentRecordsForTeacherQuery(TeacherUserId, null, null, null, null, null, Skip: 0, Take: 10),
            CancellationToken.None);

        Assert.True(page.IsSuccess);
        Assert.Equal(25, page.Value!.TotalCount);
        Assert.Equal(10, page.Value.Items.Count);
    }

    [Fact]
    public async Task Search_Filters_By_Text_On_Description()
    {
        var records = new[]
        {
            Record("Matematik — Ahmet", PaymentStatus.Pending, 1),
            Record("Fizik — Ayşe", PaymentStatus.Pending, 2),
            Record("Matematik — Zehra", PaymentStatus.Paid, 3),
        };
        var handler = NewHandler(records);

        var page = await handler.Handle(
            new SearchPaymentRecordsForTeacherQuery(TeacherUserId, "matematik", null, null, null, null, 0, 20),
            CancellationToken.None);

        Assert.Equal(2, page.Value!.TotalCount);
        Assert.All(page.Value.Items, item => Assert.Contains("Matematik", item.Description));
    }

    [Fact]
    public async Task Search_Filters_By_Overdue_Status()
    {
        var records = new[]
        {
            Record("Gecikmiş", PaymentStatus.Pending, dueDaysFromNow: -5), // vadesi geçmiş, bakiye var → Overdue
            Record("Açık", PaymentStatus.Pending, dueDaysFromNow: 5),
            Record("Ödenmiş", PaymentStatus.Paid, dueDaysFromNow: -5),
        };
        var handler = NewHandler(records);

        var page = await handler.Handle(
            new SearchPaymentRecordsForTeacherQuery(TeacherUserId, null, "Overdue", null, null, null, 0, 20),
            CancellationToken.None);

        Assert.Equal(1, page.Value!.TotalCount);
        Assert.Equal("Gecikmiş", page.Value.Items.Single().Description);
    }

    private static SearchPaymentRecordsForTeacherQueryHandler NewHandler(IReadOnlyCollection<PaymentRecord> records)
        => new(new FakeRepo(records), new FixedClock(Now));

    private static PaymentRecord Record(string description, PaymentStatus status, int dueDaysFromNow)
        => new(
            Guid.NewGuid(),
            TeacherUserId,
            studentId: Guid.NewGuid(),
            relatedLessonSessionId: null,
            BillingItemType.LessonFee,
            description: description,
            currency: "TRY",
            expectedAmount: 100m,
            collectedAmount: status == PaymentStatus.Paid ? 100m : 0m,
            dueDateUtc: Now.AddDays(dueDaysFromNow),
            collectedOnUtc: status == PaymentStatus.Paid ? Now : null,
            status: status,
            billingPeriodStartUtc: null,
            billingPeriodEndUtc: null,
            notes: null,
            createdOnUtc: Now.AddDays(-30));

    private sealed class FakeRepo(IReadOnlyCollection<PaymentRecord> records) : IPaymentRecordRepository
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
