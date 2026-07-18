using System.Text.Json;
using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Assignments.Infrastructure;

internal sealed class LessonSessionCompletedIntegrationEventHandler : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAssignmentRepository _repository;
    private readonly ILessonSessionAccessService _lessonSessionAccessService;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public LessonSessionCompletedIntegrationEventHandler(
        IAssignmentRepository repository,
        ILessonSessionAccessService lessonSessionAccessService,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _lessonSessionAccessService = lessonSessionAccessService;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public bool CanHandle(IIntegrationEvent integrationEvent)
    {
        return integrationEvent.SourceModule == "LessonSessions"
            && integrationEvent.Name == "LessonSessionCompletedDomainEvent";
    }

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent eventEnvelope)
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<LessonSessionCompletedEventPayload>(eventEnvelope.Payload, JsonOptions);
        if (payload is null)
        {
            return;
        }

        var existingNote = await _repository.GetLessonNoteByLessonSessionIdAsync(payload.LessonSessionId, cancellationToken);
        if (existingNote is not null)
        {
            return;
        }

        var lessonSession = await _lessonSessionAccessService.GetByIdAsync(payload.LessonSessionId, cancellationToken);
        if (lessonSession is null || !lessonSession.IsCompleted)
        {
            return;
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
            _clock.UtcNow);

        await _repository.AddLessonNoteAsync(note, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
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
