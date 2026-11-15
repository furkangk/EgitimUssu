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

    /// <summary>M10-4: Bu konuda tamamlanmış ders sayısı (M05 <c>LessonSessionCompleted</c> beslemesi).</summary>
    public int AttendedLessonCount { get; private set; }

    /// <summary>M10-4: Tamamlanan derslerin toplam süresi (dakika); çalışma bileşenine eklenir.</summary>
    public int TotalLessonMinutes { get; private set; }

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

    /// <summary>
    /// M10-4: Tamamlanan bir dersten katılım katkısı yazar. Ders süresi çalışma bileşenine eklenir —
    /// öğretmenle geçirilen süre de bu konudaki hâkimiyeti besler, ama <b>test bileşenine</b> dokunmaz
    /// (ders katılımı başarı ölçmez, yalnız maruziyet gösterir).
    /// </summary>
    public void RegisterLessonAttendance(int lessonMinutes, DateTime nowUtc)
    {
        AttendedLessonCount += 1;
        TotalLessonMinutes += Math.Max(0, lessonMinutes);
        UpdateSource();
        Recalculate(nowUtc);
    }

    // Kaynak, hangi beslemelerin katkı verdiğini gösterir (M08 çalışma/test + M05 ders katılımı).
    private void UpdateSource()
    {
        var hasStudy = TotalStudyMinutes > 0 || TestAttemptCount > 0;
        var hasLesson = AttendedLessonCount > 0;
        Source = (hasStudy, hasLesson) switch
        {
            (true, true) => MasterySource.Combined,
            (false, true) => MasterySource.LessonOnly,
            _ => MasterySource.StudyOnly
        };
    }

    private void Recalculate(DateTime nowUtc)
    {
        // Skor (0–100): çalışma bileşeni (maks 30, 3 saatte doyar) + test bileşeni (maks 70, ort. net oranı).
        // M10-4: ders katılım süresi çalışma bileşenine eklenir; ağırlıklar değişmez, ders yoksa davranış aynıdır.
        var engagementMinutes = TotalStudyMinutes + TotalLessonMinutes;
        var studyComponent = Math.Min(engagementMinutes / 180m, 1m) * 30m;
        var testComponent = (AverageNetRatio ?? 0m) * 70m;
        var score = Math.Round(studyComponent + testComponent, 2);
        MasteryScore = Math.Clamp(score, 0m, 100m);

        var hasData = TotalStudyMinutes > 0 || TestAttemptCount > 0 || AttendedLessonCount > 0;
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

/// <summary>M10-1: Anlık görüntü dönemi. Hafta başlangıcı Pazartesi, ay başlangıcı ayın 1'i (UTC).</summary>
public enum SnapshotPeriod { Weekly = 1, Monthly = 2 }

/// <summary>
/// M10-1: Bir öğrencinin bir ders için belirli bir dönem sonundaki gelişim fotoğrafı.
/// <see cref="TopicMastery"/> kümülatif tutulduğu için anlık görüntü, dönem <b>kapandıktan sonra</b>
/// o dersin konularının o andaki toplamını dondurur; böylece zaman serisi ve trend elde edilir.
/// Benzersizlik <c>(StudentId, Subject, Period, PeriodStart)</c> ile korunur — aynı dönem iki kez yazılmaz.
/// </summary>
public sealed class ProgressSnapshot : AggregateRoot<Guid>
{
    private ProgressSnapshot()
    {
    }

    public ProgressSnapshot(
        Guid id,
        Guid studentId,
        string subject,
        SnapshotPeriod period,
        DateOnly periodStart,
        int masteryScore,
        int studyMinutes,
        int testCount,
        decimal averageNet,
        DateTime createdOnUtc)
    {
        Id = id;
        StudentId = studentId;
        Subject = subject.Trim();
        Period = period;
        PeriodStart = periodStart;
        MasteryScore = Math.Clamp(masteryScore, 0, 100);
        StudyMinutes = Math.Max(0, studyMinutes);
        TestCount = Math.Max(0, testCount);
        AverageNet = Math.Clamp(averageNet, 0m, 1m);
        CreatedOnUtc = createdOnUtc;
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public SnapshotPeriod Period { get; private set; }

    /// <summary>Dönemin ilk günü (haftalık: Pazartesi, aylık: ayın 1'i).</summary>
    public DateOnly PeriodStart { get; private set; }

    /// <summary>Dersin konularının hâkimiyet skoru ortalaması (0–100, tam sayıya yuvarlanmış).</summary>
    public int MasteryScore { get; private set; }

    public int StudyMinutes { get; private set; }

    public int TestCount { get; private set; }

    /// <summary>Ders konularının ortalama net <b>oranı</b> (0–1); test yoksa 0.</summary>
    public decimal AverageNet { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }
}

/// <summary>M10-1: Dönem sınırı hesapları — hafta başlangıcı <b>Pazartesi</b>, tüm hesaplar UTC.</summary>
public static class SnapshotPeriods
{
    /// <summary>Verilen günün içinde bulunduğu dönemin ilk günü.</summary>
    public static DateOnly StartOf(SnapshotPeriod period, DateOnly date) => period switch
    {
        SnapshotPeriod.Weekly => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        SnapshotPeriod.Monthly => new DateOnly(date.Year, date.Month, 1),
        _ => date
    };

    /// <summary>Verilen dönem başlangıcından bir önceki dönemin başlangıcı.</summary>
    public static DateOnly Previous(SnapshotPeriod period, DateOnly periodStart) => period switch
    {
        SnapshotPeriod.Weekly => periodStart.AddDays(-7),
        SnapshotPeriod.Monthly => periodStart.AddMonths(-1),
        _ => periodStart
    };
}

/// <summary>
/// M10-1: İki ardışık anlık görüntünün hâkimiyet skoru farkından trend üretir.
/// Eşik ±<see cref="Threshold"/> puandır; önceki dönem yoksa trend <see cref="ProgressTrend.Stable"/>.
/// </summary>
public static class ProgressTrendCalculator
{
    /// <summary>Trend eşiği (puan).</summary>
    public const int Threshold = 5;

    public static ProgressTrend FromScores(int? previousScore, int currentScore)
    {
        if (previousScore is not { } previous)
        {
            return ProgressTrend.Stable;
        }

        var delta = currentScore - previous;
        return delta >= Threshold ? ProgressTrend.Improving
            : delta <= -Threshold ? ProgressTrend.Declining
            : ProgressTrend.Stable;
    }
}

/// <summary>Bir ders oturumunun katılım katkısı bir kez uygulanır; olay kimliği değişse de korunur.</summary>
public sealed class LessonAttendanceReceipt
{
    private LessonAttendanceReceipt() { }
    public LessonAttendanceReceipt(Guid lessonSessionId, DateTime appliedOnUtc)
    {
        LessonSessionId = lessonSessionId;
        AppliedOnUtc = appliedOnUtc;
    }
    public Guid LessonSessionId { get; private set; }
    public DateTime AppliedOnUtc { get; private set; }
}
