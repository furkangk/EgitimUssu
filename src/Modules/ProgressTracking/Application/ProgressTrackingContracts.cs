using EgitimUssu.Modules.ProgressTracking.Domain;

namespace EgitimUssu.Modules.ProgressTracking.Application;

/// <summary>Öğrenci-kapsamlı (sahiplik) istek işaret arayüzü; açık-generik yetkilendirici bunları korur.</summary>
public interface IStudentScopedProgressRequest
{
    Guid StudentId { get; }
}

public sealed record TopicMasteryResponse(
    Guid Id,
    Guid StudentId,
    string Subject,
    string Topic,
    string MasteryLevel,
    decimal MasteryScore,
    int TotalStudyMinutes,
    int TestAttemptCount,
    decimal? AverageNetRatio,
    string Trend,
    bool IsWeakSpot,
    bool IsStrength,
    DateTime LastEvaluatedOnUtc);

public sealed record TopicGoalResponse(
    Guid Id,
    Guid StudentId,
    string Subject,
    string Topic,
    string TargetMasteryLevel,
    decimal? TargetNetRatio,
    string SetByRole,
    DateOnly? TargetDate,
    string Status,
    DateTime? AchievedOnUtc,
    DateTime CreatedOnUtc);

public sealed record ProgressOverviewResponse(
    Guid StudentId,
    int MasteredCount,
    int ProficientCount,
    int DevelopingCount,
    int WeakCount,
    int NotStartedCount,
    int ActiveGoalCount,
    IReadOnlyCollection<TopicMasteryResponse> WeakSpots,
    IReadOnlyCollection<TopicMasteryResponse> Strengths);

/// <summary>
/// ProgressTracking kalıcılık işlemleri (tek unit-of-work). Türetilmiş veri; kaynak olaylardan beslenir.
/// </summary>
public interface IProgressRepository
{
    Task<TopicMastery?> GetMasteryAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken);

    Task<IReadOnlyList<TopicMastery>> ListMasteryAsync(Guid studentId, string? subject, CancellationToken cancellationToken);

    Task AddMasteryAsync(TopicMastery mastery, CancellationToken cancellationToken);

    Task<TopicGoal?> GetGoalAsync(Guid goalId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TopicGoal>> ListGoalsAsync(Guid studentId, TopicGoalStatus? status, CancellationToken cancellationToken);

    Task<IReadOnlyList<TopicGoal>> ListActiveGoalsForTopicAsync(Guid studentId, string subject, string topic, CancellationToken cancellationToken);

    Task AddGoalAsync(TopicGoal goal, CancellationToken cancellationToken);

    Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken);

    Task AddProcessedAsync(ProcessedEvent processed, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
