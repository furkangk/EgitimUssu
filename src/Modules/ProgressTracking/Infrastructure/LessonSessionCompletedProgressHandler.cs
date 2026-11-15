using System.Text.Json;
using EgitimUssu.Modules.ProgressTracking.Application;
using EgitimUssu.Modules.ProgressTracking.Domain;
using Microsoft.EntityFrameworkCore;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.ProgressTracking.Infrastructure;

/// <summary>
/// M10-4: LessonSessions <c>LessonSessionCompletedDomainEvent</c> → ilgili ders+konu hâkimiyetine
/// <b>katılım</b> katkısı yazar (M05 beslemesi). Idempotent: aynı olay iki kez işlenmez.
/// <para>
/// Olay yükü ders/branş ve konu taşımaz; ikisi de <see cref="ILessonScheduleDirectory"/> (branş + süre)
/// ve <see cref="ILessonSessionAccessService"/> (konu başlığı) sözleşmelerinden okunur — modül sınırı
/// korunur, ProgressTracking başka bir modülün DbContext'ine bakmaz.
/// </para>
/// <para>
/// Plansız (ad-hoc) oturumda planlı ders yoktur → branş bilinemez. Böyle bir olay <b>işlenmiş sayılır</b>
/// (inbox'a yazılır) ama satır üretilmez: aksi hâlde outbox bu mesajı sonsuza dek yeniden denerdi
/// (Öğrenilenler #70 ile aynı gerekçe).
/// </para>
/// </summary>
internal sealed class LessonSessionCompletedProgressHandler : IdempotentIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationEventSerialization.Options;
    private readonly MasteryService _masteryService;
    private readonly ILessonScheduleDirectory _schedules;
    private readonly ILessonSessionAccessService _sessions;

    public LessonSessionCompletedProgressHandler(
        ProgressTrackingDbContext dbContext,
        MasteryService masteryService,
        ILessonScheduleDirectory schedules,
        ILessonSessionAccessService sessions,
        IClock clock)
        : base(dbContext, clock)
    {
        _masteryService = masteryService;
        _schedules = schedules;
        _sessions = sessions;
    }

    public override bool CanHandle(IIntegrationEvent integrationEvent) =>
        integrationEvent.SourceModule == "LessonSessions"
        && integrationEvent.Name == "LessonSessionCompletedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LessonSessionCompletedPayload>(envelope.Payload, JsonOptions);
        if (payload is null || payload.StudentId == Guid.Empty)
        {
            throw new PoisonMessageException("Ders tamamlanma olay yükü geçersiz.");
        }

        if (payload.LessonScheduleId is not { } scheduleId)
        {
            return true; // plansız oturum: branş yok, katkı yazılamaz
        }

        var schedule = await _schedules.GetAsync(scheduleId, cancellationToken);
        if (schedule is null) throw new InvalidOperationException("Ders planı henüz okunamıyor.");
        if (string.IsNullOrWhiteSpace(schedule.Subject))
        {
            return true;
        }

        // Konu başlığı oturumda tutulur; boşsa MasteryService "(Genel)" konusuna düşer.
        var session = await _sessions.GetByIdAsync(payload.LessonSessionId, cancellationToken);
        var db = (ProgressTrackingDbContext)DbContext;
        if (session is null) throw new InvalidOperationException("Ders oturumu henüz okunamıyor.");
        if (!session.IsCompleted || session.AttendanceStatus is not ("Attended" or "Late")) return true;
        if (await db.LessonAttendanceReceipts.AnyAsync(item => item.LessonSessionId == payload.LessonSessionId, cancellationToken)) return true;
        db.LessonAttendanceReceipts.Add(new LessonAttendanceReceipt(payload.LessonSessionId, Clock.UtcNow));
        var topic = string.IsNullOrWhiteSpace(session.TopicTitle) ? null : session!.TopicTitle;

        var lessonMinutes = (int)Math.Round((schedule.EndAtUtc - schedule.StartAtUtc).TotalMinutes);
        await _masteryService.ApplyLessonAttendanceAsync(
            payload.StudentId, schedule.Subject, topic, Math.Max(0, lessonMinutes), cancellationToken);
        return true;
    }

    private sealed record LessonSessionCompletedPayload(
        Guid LessonSessionId,
        Guid? LessonScheduleId,
        Guid TeacherUserId,
        Guid StudentId,
        DateTime CompletedOnUtc);
}
