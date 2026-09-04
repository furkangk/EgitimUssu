using System.Text.Json;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Payments.Infrastructure;

/// <summary>
/// M07-2: Tamamlanan **ücretli** ders oturumu için otomatik ödeme kaydı üretir.
/// Modül sınırı korunur: LessonSessions'ın DbContext'i okunmaz, yalnız integration event +
/// <see cref="ILessonSessionAccessService"/> / <see cref="IStudentDirectory"/> sözleşmeleri kullanılır.
/// Replay koruması ortak inbox üzerinden (<see cref="IdempotentIntegrationEventHandler"/>, EventId+Handler);
/// ayrıca <b>oturum başına tek ödeme kaydı</b> iş kuralı korunur (ders yeniden tamamlanabildiği için
/// aynı oturum farklı EventId ile ikinci kez gelebilir).
/// </summary>
public sealed class LessonSessionCompletedPaymentHandler : IdempotentIntegrationEventHandler
{
    /// <summary>Ders ücretinin vadesi: ders tamamlandıktan bu kadar gün sonra (varsayılan ödeme vadesi).</summary>
    private const int LessonFeeDueDays = 7;

    private readonly ILessonSessionAccessService _lessonSessions;
    private readonly IStudentDirectory _students;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<LessonSessionCompletedPaymentHandler> _logger;

    public LessonSessionCompletedPaymentHandler(
        PaymentsDbContext dbContext,
        ILessonSessionAccessService lessonSessions,
        IStudentDirectory students,
        IIdGenerator idGenerator,
        IClock clock,
        ILogger<LessonSessionCompletedPaymentHandler> logger)
        : base(dbContext, clock)
    {
        _lessonSessions = lessonSessions;
        _students = students;
        _idGenerator = idGenerator;
        _logger = logger;
    }

    private PaymentsDbContext PaymentsDb => (PaymentsDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "LessonSessions"
            && integrationEvent.Name == "LessonSessionCompletedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LessonSessionCompletedEventPayload>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            return false;
        }

        // İş kuralı: oturum başına tek ödeme kaydı.
        var alreadyBilled = await PaymentsDb.PaymentRecords
            .AnyAsync(record => record.RelatedLessonSessionId == payload.LessonSessionId, cancellationToken);
        if (alreadyBilled)
        {
            return false;
        }

        var session = await _lessonSessions.GetByIdAsync(payload.LessonSessionId, cancellationToken);
        if (session is null || !session.IsCompleted || !session.IsChargeable)
        {
            return false;
        }

        var rate = await _students.GetAgreedLessonRateAsync(session.TeacherUserId, session.StudentId, cancellationToken);
        if (rate is null)
        {
            // Sessiz yanlış tutar üretmektense kayıt açma; öğretmen ücreti girince elle ekleyebilir.
            _logger.LogInformation(
                "Lesson session {LessonSessionId} is chargeable but no agreed rate exists for teacher {TeacherUserId} / student {StudentId}; no payment record created.",
                session.Id,
                session.TeacherUserId,
                session.StudentId);
            return false;
        }

        var completedOnUtc = session.CompletedOnUtc ?? payload.CompletedOnUtc;
        var record = new PaymentRecord(
            _idGenerator.New(),
            session.TeacherUserId,
            session.StudentId,
            session.Id,
            BillingItemType.LessonFee,
            BuildDescription(session),
            rate.Currency,
            expectedAmount: rate.Amount,
            collectedAmount: 0m,
            dueDateUtc: completedOnUtc.AddDays(LessonFeeDueDays),
            collectedOnUtc: null,
            PaymentStatus.Pending,
            billingPeriodStartUtc: null,
            billingPeriodEndUtc: null,
            notes: null,
            createdOnUtc: Clock.UtcNow);

        PaymentsDb.PaymentRecords.Add(record);
        return true;
    }

    private static string BuildDescription(LessonSessionDetails session)
    {
        var topic = session.TopicTitle.Trim();
        return string.IsNullOrEmpty(topic) ? "Ders ücreti" : $"Ders ücreti — {topic}";
    }

    private sealed record LessonSessionCompletedEventPayload(
        Guid LessonSessionId,
        Guid? LessonScheduleId,
        Guid TeacherUserId,
        Guid StudentId,
        DateTime CompletedOnUtc);
}
