using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.ProgressTracking.Domain;

/// <summary>
/// Öğrencinin belirli bir ders+konu için güncel hâkimiyet seviyesi. Türetilmiş veridir:
/// M08 çalışma süresi + test netlerinden beslenir (kendi ölçümünü üretmez).
/// </summary>
public sealed class TopicMastery : AggregateRoot<Guid>
{
    private TopicMastery()
    {
    }

    public TopicMastery(Guid id, Guid studentId, string subject, string topic, DateTime nowUtc)
    {
        Id = id;
        StudentId = studentId;
        Subject = subject.Trim();
        Topic = topic.Trim();
        MasteryLevel = MasteryLevel.NotStarted;
        Trend = ProgressTrend.Stable;
        LastEvaluatedOnUtc = nowUtc;
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Topic { get; private set; } = string.Empty;

    public MasteryLevel MasteryLevel { get; private set; }

    public decimal MasteryScore { get; private set; }

    public int TotalStudyMinutes { get; private set; }

    public int TestAttemptCount { get; private set; }

    public decimal? AverageNetRatio { get; private set; }

    /// <summary>Net oranı toplamı (ortalama = / TestAttemptCount). Yeniden hesaplama için tutulur.</summary>
    public decimal NetRatioSum { get; private set; }

    public decimal? RecentNetRatio { get; private set; }

    public decimal? PriorNetRatio { get; private set; }

    public ProgressTrend Trend { get; private set; }

    public bool IsWeakSpot { get; private set; }

    public bool IsStrength { get; private set; }

    public MasterySource Source { get; private set; }

    public DateTime LastEvaluatedOnUtc { get; private set; }

    /// <summary>Tamamlanan bir çalışma seansından süre ekler ve hâkimiyeti yeniden hesaplar.</summary>
    public void RegisterStudy(int effectiveMinutes, DateTime nowUtc)
    {
        TotalStudyMinutes += Math.Max(0, effectiveMinutes);
        UpdateSource();
        Recalculate(nowUtc);
    }

    /// <summary>Bir test sonucundan net oranını işler ve hâkimiyeti yeniden hesaplar.</summary>
    public void RegisterTest(int totalQuestions, decimal net, DateTime nowUtc)
    {
        var ratio = totalQuestions <= 0 ? 0m : Math.Clamp(net / totalQuestions, 0m, 1m);
        TestAttemptCount += 1;
        NetRatioSum += ratio;
        AverageNetRatio = Math.Round(NetRatioSum / TestAttemptCount, 4);
        PriorNetRatio = RecentNetRatio;
        RecentNetRatio = ratio;
        UpdateSource();
        Recalculate(nowUtc);
    }

    // M08 (çalışma + test) beslemesi. M05 ders beslemesi eklenirse LessonOnly/Combined ayrımı genişler.
    private void UpdateSource() => Source = MasterySource.StudyOnly;

    private void Recalculate(DateTime nowUtc)
    {
        // Skor (0–100): çalışma bileşeni (maks 30, 3 saatte doyar) + test bileşeni (maks 70, ort. net oranı).
        var studyComponent = Math.Min(TotalStudyMinutes / 180m, 1m) * 30m;
        var testComponent = (AverageNetRatio ?? 0m) * 70m;
        var score = Math.Round(studyComponent + testComponent, 2);
        MasteryScore = Math.Clamp(score, 0m, 100m);

        var hasData = TotalStudyMinutes > 0 || TestAttemptCount > 0;
        MasteryLevel = !hasData ? MasteryLevel.NotStarted
            : MasteryScore < 20m ? MasteryLevel.Weak
            : MasteryScore < 45m ? MasteryLevel.Developing
            : MasteryScore < 75m ? MasteryLevel.Proficient
            : MasteryLevel.Mastered;

        Trend = (RecentNetRatio, PriorNetRatio) switch
        {
            ({ } r, { } p) when r > p + 0.02m => ProgressTrend.Improving,
            ({ } r, { } p) when r < p - 0.02m => ProgressTrend.Declining,
            _ => ProgressTrend.Stable
        };

        IsWeakSpot = MasteryLevel is MasteryLevel.Weak or MasteryLevel.Developing || Trend == ProgressTrend.Declining;
        IsStrength = MasteryLevel is MasteryLevel.Proficient or MasteryLevel.Mastered
            && Trend is ProgressTrend.Stable or ProgressTrend.Improving;

        LastEvaluatedOnUtc = nowUtc;

        Raise(new TopicMasteryChangedDomainEvent(Id, StudentId, Subject, Topic, MasteryLevel.ToString(), MasteryScore, nowUtc));
    }
}

/// <summary>Bir konu için belirlenen hedef seviye/net. Öğrenci kendisi koyabilir (ileride öğretmen de).</summary>
public sealed class TopicGoal : AggregateRoot<Guid>
{
    private TopicGoal()
    {
    }

    public TopicGoal(
        Guid id,
        Guid studentId,
        string subject,
        string topic,
        MasteryLevel targetMasteryLevel,
        decimal? targetNetRatio,
        Guid setByUserId,
        TopicGoalSetterRole setByRole,
        DateOnly? targetDate,
        DateTime nowUtc)
    {
        Id = id;
        StudentId = studentId;
        Subject = subject.Trim();
        Topic = topic.Trim();
        TargetMasteryLevel = targetMasteryLevel;
        TargetNetRatio = targetNetRatio;
        SetByUserId = setByUserId;
        SetByRole = setByRole;
        TargetDate = targetDate;
        Status = TopicGoalStatus.Active;
        CreatedOnUtc = nowUtc;
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Topic { get; private set; } = string.Empty;

    public MasteryLevel TargetMasteryLevel { get; private set; }

    public decimal? TargetNetRatio { get; private set; }

    public Guid SetByUserId { get; private set; }

    public TopicGoalSetterRole SetByRole { get; private set; }

    public DateOnly? TargetDate { get; private set; }

    public TopicGoalStatus Status { get; private set; }

    public DateTime? AchievedOnUtc { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public void MarkAchieved(DateTime nowUtc)
    {
        if (Status != TopicGoalStatus.Active)
        {
            return;
        }

        Status = TopicGoalStatus.Achieved;
        AchievedOnUtc = nowUtc;
        Raise(new TopicGoalAchievedDomainEvent(Id, StudentId, Subject, Topic, nowUtc));
    }

    public void Cancel()
    {
        if (Status == TopicGoalStatus.Active)
        {
            Status = TopicGoalStatus.Cancelled;
        }
    }
}

public enum MasteryLevel { NotStarted = 1, Weak = 2, Developing = 3, Proficient = 4, Mastered = 5 }

public enum ProgressTrend { Improving = 1, Stable = 2, Declining = 3 }

public enum MasterySource { StudyOnly = 1, LessonOnly = 2, Combined = 3 }

public enum TopicGoalStatus { Active = 1, Achieved = 2, Missed = 3, Cancelled = 4 }

public enum TopicGoalSetterRole { Student = 1, Teacher = 2 }

public sealed record TopicMasteryChangedDomainEvent(
    Guid TopicMasteryId,
    Guid StudentId,
    string Subject,
    string Topic,
    string MasteryLevel,
    decimal MasteryScore,
    DateTime EvaluatedOnUtc) : DomainEvent;

public sealed record TopicGoalAchievedDomainEvent(
    Guid TopicGoalId,
    Guid StudentId,
    string Subject,
    string Topic,
    DateTime AchievedOnUtc) : DomainEvent;
