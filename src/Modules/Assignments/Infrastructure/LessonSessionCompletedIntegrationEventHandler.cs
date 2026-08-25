using System.Text.Json;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

/// <summary>
/// LessonSessions → tamamlanan derse otomatik ders notu (follow-up) üretici. Replay koruması artık
/// ortak inbox üzerinden (<see cref="IdempotentIntegrationEventHandler"/>, EventId+Handler);
/// LessonSessionId başına TEK ders notu kuralı (unique index, <c>lesson_notes</c>) gerçek bir iş
/// kuralı olduğundan burada korunur — zaten not varsa yeniden üretilmez.
/// </summary>
internal sealed class LessonSessionCompletedIntegrationEventHandler : IdempotentIntegrationEventHandler
{
    private readonly ILessonSessionAccessService _lessonSessionAccessService;
    private readonly IIdGenerator _idGenerator;

    public LessonSessionCompletedIntegrationEventHandler(
        AssignmentsDbContext dbContext,
        ILessonSessionAccessService lessonSessionAccessService,
        IIdGenerator idGenerator,
        IClock clock)
        : base(dbContext, clock)
    {
        _lessonSessionAccessService = lessonSessionAccessService;
        _idGenerator = idGenerator;
    }

    private AssignmentsDbContext AssignmentsDb => (AssignmentsDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
    {
        return integrationEvent.SourceModule == "LessonSessions"
            && integrationEvent.Name == "LessonSessionCompletedDomainEvent";
    }

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<LessonSessionCompletedEventPayload>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            return false;
        }

        // İş kuralı (unique index): LessonSessionId başına tek ders notu. Zaten varsa yeniden üretilmez.
        var existingNote = await AssignmentsDb.LessonNotes
            .AnyAsync(note => note.LessonSessionId == payload.LessonSessionId, cancellationToken);
        if (existingNote)
        {
            return false;
        }

        var lessonSession = await _lessonSessionAccessService.GetByIdAsync(payload.LessonSessionId, cancellationToken);
        if (lessonSession is null || !lessonSession.IsCompleted)
        {
            return false;
        }

        var summary = BuildSummary(lessonSession);
        var note = new LessonNote(
            _idGenerator.New(),
            lessonSession.Id,
            lessonSession.TeacherUserId,
            lessonSession.StudentId,
            summary,
            lessonSession.CoveredContent,
            lessonSession.TeacherNotes,
            LessonNoteVisibility.Private,
            Clock.UtcNow);

        AssignmentsDb.LessonNotes.Add(note);
        return true;
    }

    private static string BuildSummary(LessonSessionDetails lessonSession)
    {
        if (!string.IsNullOrWhiteSpace(lessonSession.TeacherNotes))
        {
            return lessonSession.TeacherNotes.Trim();
        }

        if (!string.IsNullOrWhiteSpace(lessonSession.CoveredContent))
        {
            return lessonSession.CoveredContent.Trim();
        }

        return $"{lessonSession.TopicTitle.Trim()} konusu tamamlandi.";
    }

    private sealed record LessonSessionCompletedEventPayload(
        Guid LessonSessionId,
        Guid? LessonScheduleId,
        Guid TeacherUserId,
        Guid StudentId,
        DateTime CompletedOnUtc);
}
