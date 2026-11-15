namespace EgitimUssu.Shared.Contracts;

/// <summary>Study'nin güncel istatistik kaynağı; silinen kayıtlar dönmez.</summary>
public interface IStudyStatisticsReader
{
    Task<StudyStatistics> ReadAsync(Guid studentId, CancellationToken cancellationToken);
}

public sealed record StudyStatistics(IReadOnlyList<StudySessionStatistic> Sessions, IReadOnlyList<StudyTestStatistic> Tests);
public sealed record StudySessionStatistic(Guid Id, string Subject, string? Topic, int EffectiveMinutes, DateTime EndedAtUtc);
public sealed record StudyTestStatistic(Guid Id, string Subject, string? Topic, int TotalQuestions, decimal Net, DateTime TakenOnUtc);

/// <summary>Eski olay yükünü değiştirmeden ekleme ve düzeltme bildirimlerini tanır.</summary>
public static class StudyStatisticsEvents
{
    public static bool Matches(IIntegrationEvent message) => message.SourceModule == "Study" && message.Name is
        "StudySessionCompletedDomainEvent" or "StudySessionEditedDomainEvent" or "StudySessionDeletedDomainEvent"
        or "TestResultRecordedDomainEvent" or "TestResultEditedDomainEvent" or "TestResultDeletedDomainEvent";
}
