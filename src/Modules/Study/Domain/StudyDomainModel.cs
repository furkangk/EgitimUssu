using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Study.Domain;

/// <summary>
/// Çalışma seansı (kronometre). Mola süresi net çalışmaya (EffectiveMinutes) dahil edilmez.
/// </summary>
public sealed class StudySession : AggregateRoot<Guid>
{
    private StudySession()
    {
    }

    private StudySession(
        Guid id,
        Guid studentId,
        string subject,
        string? topic,
        DateTime startedAtUtc,
        StudySessionSource source,
        StudySessionStatus status,
        int effectiveMinutes,
        int breakMinutes,
        DateTime? endedAtUtc,
        string? personalNote,
        bool isSharedWithParent,
        bool isSharedWithTeacher,
        DateTime createdOnUtc)
    {
        Id = id;
        StudentId = studentId;
        Subject = subject;
        Topic = topic;
        StartedAtUtc = startedAtUtc;
        Source = source;
        Status = status;
        EffectiveMinutes = effectiveMinutes;
        BreakMinutes = breakMinutes;
        EndedAtUtc = endedAtUtc;
        PersonalNote = personalNote;
        LastResumedAtUtc = status == StudySessionStatus.Running ? startedAtUtc : null;
        IsSharedWithParent = isSharedWithParent;
        IsSharedWithTeacher = isSharedWithTeacher;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string? Topic { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }

    public int EffectiveMinutes { get; private set; }

    public int BreakMinutes { get; private set; }

    public StudySessionStatus Status { get; private set; }

    public DateTime? LastResumedAtUtc { get; private set; }

    public DateTime? LastPausedAtUtc { get; private set; }

    public string? PersonalNote { get; private set; }

    public StudySessionSource Source { get; private set; }

    public bool IsSharedWithParent { get; private set; }

    public bool IsSharedWithTeacher { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    /// <summary>Kronometreyle yeni bir çalışma seansı başlatır (Status = Running).</summary>
    public static StudySession StartStopwatch(
        Guid id,
        Guid studentId,
        string subject,
        string? topic,
        bool isSharedWithParent,
        bool isSharedWithTeacher,
        DateTime nowUtc)
    {
        var session = new StudySession(
            id,
            studentId,
            subject,
            topic,
            nowUtc,
            StudySessionSource.Stopwatch,
            StudySessionStatus.Running,
            effectiveMinutes: 0,
            breakMinutes: 0,
            endedAtUtc: null,
            personalNote: null,
            isSharedWithParent,
            isSharedWithTeacher,
            nowUtc);

        session.Raise(new StudySessionStartedDomainEvent(id, studentId, subject, nowUtc));
        return session;
    }

    /// <summary>Kronometre kullanmadan geçmişe dönük tamamlanmış seans girişi (Source = Manual).</summary>
    public static StudySession CreateManual(
        Guid id,
        Guid studentId,
        string subject,
        string? topic,
        int effectiveMinutes,
        DateTime studiedOnUtc,
        string? personalNote,
        bool isSharedWithParent,
        bool isSharedWithTeacher,
        DateTime nowUtc)
    {
        if (effectiveMinutes <= 0)
        {
            throw new InvalidOperationException("Manuel seans süresi 0'dan büyük olmalıdır.");
        }

        var session = new StudySession(
            id,
            studentId,
            subject,
            topic,
            studiedOnUtc,
            StudySessionSource.Manual,
            StudySessionStatus.Completed,
            effectiveMinutes,
            breakMinutes: 0,
            endedAtUtc: studiedOnUtc,
            personalNote,
            isSharedWithParent,
            isSharedWithTeacher,
            nowUtc);

        session.Raise(new StudySessionCompletedDomainEvent(
            id, studentId, subject, topic, effectiveMinutes, 0, studiedOnUtc));
        return session;
    }

    /// <summary>Aktif dilimi net süreye ekler ve seansı molaya alır.</summary>
    public void Pause(DateTime nowUtc)
    {
        if (Status != StudySessionStatus.Running)
        {
            throw new InvalidOperationException("Yalnızca çalışan bir seans molaya alınabilir.");
        }

        EffectiveMinutes += MinutesBetween(LastResumedAtUtc ?? StartedAtUtc, nowUtc);
        Status = StudySessionStatus.Paused;
        LastPausedAtUtc = nowUtc;
        LastResumedAtUtc = null;
        UpdatedOnUtc = nowUtc;
    }

    /// <summary>Mola süresini BreakMinutes'e ekler ve seansı sürdürür.</summary>
    public void Resume(DateTime nowUtc)
    {
        if (Status != StudySessionStatus.Paused)
        {
            throw new InvalidOperationException("Yalnızca moladaki bir seans sürdürülebilir.");
        }

        BreakMinutes += MinutesBetween(LastPausedAtUtc ?? nowUtc, nowUtc);
        Status = StudySessionStatus.Running;
        LastResumedAtUtc = nowUtc;
        LastPausedAtUtc = null;
        UpdatedOnUtc = nowUtc;
    }

    /// <summary>Son aktif/mola dilimini kapatır, seansı tamamlar ve tamamlanma olayını yükseltir.</summary>
    public void Complete(DateTime nowUtc, string? personalNote)
    {
        if (Status is not (StudySessionStatus.Running or StudySessionStatus.Paused))
        {
            throw new InvalidOperationException("Yalnızca aktif (çalışan/molada) bir seans tamamlanabilir.");
        }

        if (Status == StudySessionStatus.Running)
        {
            EffectiveMinutes += MinutesBetween(LastResumedAtUtc ?? StartedAtUtc, nowUtc);
        }
        else
        {
            BreakMinutes += MinutesBetween(LastPausedAtUtc ?? nowUtc, nowUtc);
        }

        Status = StudySessionStatus.Completed;
        EndedAtUtc = nowUtc;
        LastResumedAtUtc = null;
        LastPausedAtUtc = null;
        PersonalNote = string.IsNullOrWhiteSpace(personalNote) ? PersonalNote : personalNote.Trim();
        UpdatedOnUtc = nowUtc;

        Raise(new StudySessionCompletedDomainEvent(
            Id, StudentId, Subject, Topic, EffectiveMinutes, BreakMinutes, nowUtc));
    }

    /// <summary>Yanlış başlatılan seansı iptal eder; hiçbir istatistiğe dahil edilmez.</summary>
    public void Discard(DateTime nowUtc)
    {
        if (Status == StudySessionStatus.Completed)
        {
            throw new InvalidOperationException("Tamamlanmış seans iptal edilemez.");
        }

        Status = StudySessionStatus.Discarded;
        EndedAtUtc = nowUtc;
        UpdatedOnUtc = nowUtc;
    }

    private static int MinutesBetween(DateTime from, DateTime to)
    {
        var minutes = (int)Math.Round((to - from).TotalMinutes, MidpointRounding.AwayFromZero);
        return Math.Max(0, minutes);
    }
}

/// <summary>
/// Deneme / sınav (test) sonucu. Net artış/azalış analizinin ham verisidir.
/// </summary>
public sealed class TestResult : AggregateRoot<Guid>
{
    private TestResult()
    {
    }

    public TestResult(
        Guid id,
        Guid studentId,
        string subject,
        string? topic,
        string? testName,
        TestType testType,
        int totalQuestions,
        int correct,
        int wrong,
        int blank,
        int penaltyDivisor,
        int? durationMinutes,
        DateTime takenOnUtc,
        bool isSharedWithParent,
        bool isSharedWithTeacher,
        DateTime createdOnUtc)
    {
        if (correct < 0 || wrong < 0 || blank < 0 || totalQuestions < 0)
        {
            throw new InvalidOperationException("Soru sayıları negatif olamaz.");
        }

        if (correct + wrong + blank != totalQuestions)
        {
            throw new InvalidOperationException("Doğru + Yanlış + Boş, toplam soru sayısına eşit olmalıdır.");
        }

        if (penaltyDivisor <= 0)
        {
            penaltyDivisor = 4;
        }

        Id = id;
        StudentId = studentId;
        Subject = subject;
        Topic = topic;
        TestName = testName;
        TestType = testType;
        TotalQuestions = totalQuestions;
        Correct = correct;
        Wrong = wrong;
        Blank = blank;
        PenaltyDivisor = penaltyDivisor;
        Net = Math.Round(correct - ((decimal)wrong / penaltyDivisor), 2, MidpointRounding.AwayFromZero);
        DurationMinutes = durationMinutes;
        TakenOnUtc = takenOnUtc;
        IsSharedWithParent = isSharedWithParent;
        IsSharedWithTeacher = isSharedWithTeacher;
        CreatedOnUtc = createdOnUtc;

        Raise(new TestResultRecordedDomainEvent(Id, StudentId, Subject, Topic, TotalQuestions, Correct, Wrong, Blank, Net, TakenOnUtc));
    }

    /// <summary>Test kaydını düzenler; net (D − Y/ceza) yeniden hesaplanır.</summary>
    public void Edit(
        string subject,
        string? topic,
        string? testName,
        TestType testType,
        int totalQuestions,
        int correct,
        int wrong,
        int blank,
        int penaltyDivisor,
        int? durationMinutes,
        DateTime takenOnUtc,
        DateTime nowUtc)
    {
        if (correct < 0 || wrong < 0 || blank < 0 || totalQuestions < 0)
        {
            throw new InvalidOperationException("Soru sayıları negatif olamaz.");
        }

        if (correct + wrong + blank != totalQuestions)
        {
            throw new InvalidOperationException("Doğru + Yanlış + Boş, toplam soru sayısına eşit olmalıdır.");
        }

        if (penaltyDivisor <= 0)
        {
            penaltyDivisor = 4;
        }

        Subject = subject.Trim();
        Topic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
        TestName = string.IsNullOrWhiteSpace(testName) ? null : testName.Trim();
        TestType = testType;
        TotalQuestions = totalQuestions;
        Correct = correct;
        Wrong = wrong;
        Blank = blank;
        PenaltyDivisor = penaltyDivisor;
        Net = Math.Round(correct - ((decimal)wrong / penaltyDivisor), 2, MidpointRounding.AwayFromZero);
        DurationMinutes = durationMinutes;
        TakenOnUtc = takenOnUtc;
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string? Topic { get; private set; }

    public string? TestName { get; private set; }

    public TestType TestType { get; private set; }

    public int TotalQuestions { get; private set; }

    public int Correct { get; private set; }

    public int Wrong { get; private set; }

    public int Blank { get; private set; }

    public decimal Net { get; private set; }

    public int PenaltyDivisor { get; private set; }

    public int? DurationMinutes { get; private set; }

    public DateTime TakenOnUtc { get; private set; }

    public bool IsSharedWithParent { get; private set; }

    public bool IsSharedWithTeacher { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }
}

/// <summary>
/// Öğrencinin kendine koyduğu çalışma hedefleri. Öğrenci başına tek aktif kayıt.
/// </summary>
public sealed class StudyGoal : AggregateRoot<Guid>
{
    private StudyGoal()
    {
    }

    public StudyGoal(
        Guid id,
        Guid studentId,
        int dailyGoalMinutes,
        int? weeklyGoalMinutes,
        decimal? targetNet,
        decimal? targetScore,
        string? subject,
        int streakThresholdPercent,
        DateTime effectiveFromUtc)
    {
        Id = id;
        StudentId = studentId;
        DailyGoalMinutes = dailyGoalMinutes;
        WeeklyGoalMinutes = weeklyGoalMinutes;
        TargetNet = targetNet;
        TargetScore = targetScore;
        Subject = subject;
        StreakThresholdPercent = Math.Clamp(streakThresholdPercent <= 0 ? 60 : streakThresholdPercent, 1, 100);
        EffectiveFromUtc = effectiveFromUtc;
        IsActive = true;
        CreatedOnUtc = effectiveFromUtc;
        UpdatedOnUtc = effectiveFromUtc;
    }

    public Guid StudentId { get; private set; }

    public int DailyGoalMinutes { get; private set; }

    public int? WeeklyGoalMinutes { get; private set; }

    public decimal? TargetNet { get; private set; }

    public decimal? TargetScore { get; private set; }

    public string? Subject { get; private set; }

    /// <summary>Günün streak'e sayılması için günlük hedefin tamamlanması gereken yüzdesi (1-100, varsayılan 60).</summary>
    public int StreakThresholdPercent { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void UpdateGoals(
        int dailyGoalMinutes,
        int? weeklyGoalMinutes,
        decimal? targetNet,
        decimal? targetScore,
        string? subject,
        int streakThresholdPercent,
        DateTime nowUtc)
    {
        DailyGoalMinutes = dailyGoalMinutes;
        WeeklyGoalMinutes = weeklyGoalMinutes;
        TargetNet = targetNet;
        TargetScore = targetScore;
        Subject = subject;
        StreakThresholdPercent = Math.Clamp(streakThresholdPercent <= 0 ? 60 : streakThresholdPercent, 1, 100);
        IsActive = true;
        UpdatedOnUtc = nowUtc;

        Raise(new StudyGoalUpdatedDomainEvent(Id, StudentId, dailyGoalMinutes, nowUtc));
    }
}

/// <summary>
/// Çalışma serisi (streak) ve kişisel rekor. Öğrenci başına tekildir.
/// </summary>
public sealed class StudyStreak : AggregateRoot<Guid>
{
    private StudyStreak()
    {
    }

    public StudyStreak(Guid id, Guid studentId, DateTime nowUtc)
    {
        Id = id;
        StudentId = studentId;
        CurrentStreakDays = 0;
        LongestStreakDays = 0;
        TotalStudyDays = 0;
        UpdatedOnUtc = nowUtc;
    }

    public Guid StudentId { get; private set; }

    public int CurrentStreakDays { get; private set; }

    public int LongestStreakDays { get; private set; }

    public DateOnly? LastStudiedOnDate { get; private set; }

    public int TotalStudyDays { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    /// <summary>Bir çalışma gününü seriye işler. Aynı gün ise no-op.</summary>
    public void RegisterStudyDay(DateOnly localDate, DateTime nowUtc)
    {
        if (LastStudiedOnDate == localDate)
        {
            return;
        }

        if (LastStudiedOnDate is { } last)
        {
            if (last.AddDays(1) == localDate)
            {
                CurrentStreakDays += 1;
            }
            else if (localDate > last)
            {
                Raise(new StreakBrokenDomainEvent(Id, StudentId, CurrentStreakDays));
                CurrentStreakDays = 1;
            }
            else
            {
                // Geçmiş bir gün girildi; seriyi bozmadan toplam günü sayar.
                TotalStudyDays += 1;
                UpdatedOnUtc = nowUtc;
                return;
            }
        }
        else
        {
            CurrentStreakDays = 1;
        }

        TotalStudyDays += 1;
        LastStudiedOnDate = localDate;

        if (CurrentStreakDays > LongestStreakDays)
        {
            LongestStreakDays = CurrentStreakDays;
            Raise(new StreakMilestoneReachedDomainEvent(Id, StudentId, CurrentStreakDays, LongestStreakDays));
        }

        UpdatedOnUtc = nowUtc;
    }
}

/// <summary>
/// Başarım rozeti katalog kaydı (statik tanım).
/// </summary>
public sealed class Achievement : Entity<Guid>
{
    private Achievement()
    {
    }

    public Achievement(
        Guid id,
        string code,
        string title,
        string description,
        AchievementCategory category,
        int threshold,
        string? iconKey)
    {
        Id = id;
        Code = code;
        Title = title;
        Description = description;
        Category = category;
        Threshold = threshold;
        IconKey = iconKey;
    }

    public string Code { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public AchievementCategory Category { get; private set; }

    public int Threshold { get; private set; }

    public string? IconKey { get; private set; }
}

/// <summary>
/// Öğrencinin bir başarımı kazanma kaydı.
/// </summary>
public sealed class StudentAchievement : AggregateRoot<Guid>
{
    private StudentAchievement()
    {
    }

    public StudentAchievement(
        Guid id,
        Guid studentId,
        Guid achievementId,
        string achievementCode,
        int progressValue,
        DateTime earnedOnUtc)
    {
        Id = id;
        StudentId = studentId;
        AchievementId = achievementId;
        AchievementCode = achievementCode;
        ProgressValue = progressValue;
        EarnedOnUtc = earnedOnUtc;

        Raise(new AchievementEarnedDomainEvent(Id, StudentId, achievementCode, earnedOnUtc));
    }

    public Guid StudentId { get; private set; }

    public Guid AchievementId { get; private set; }

    public string AchievementCode { get; private set; } = string.Empty;

    public int ProgressValue { get; private set; }

    public DateTime EarnedOnUtc { get; private set; }
}

/// <summary>
/// Öğrencinin çalıştığı konuları normalize eden hafif rollup kaydı.
/// </summary>
public sealed class StudyTopic : Entity<Guid>
{
    private StudyTopic()
    {
    }

    public StudyTopic(
        Guid id,
        Guid studentId,
        string subject,
        string topic,
        int effectiveMinutes,
        DateTime studiedOnUtc)
    {
        Id = id;
        StudentId = studentId;
        Subject = subject;
        Topic = topic;
        TotalEffectiveMinutes = effectiveMinutes;
        SessionCount = 1;
        FirstStudiedOnUtc = studiedOnUtc;
        LastStudiedOnUtc = studiedOnUtc;
    }

    public Guid StudentId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Topic { get; private set; } = string.Empty;

    public DateTime FirstStudiedOnUtc { get; private set; }

    public DateTime LastStudiedOnUtc { get; private set; }

    public int TotalEffectiveMinutes { get; private set; }

    public int SessionCount { get; private set; }

    public void RegisterStudy(int effectiveMinutes, DateTime studiedOnUtc)
    {
        TotalEffectiveMinutes += effectiveMinutes;
        SessionCount += 1;
        LastStudiedOnUtc = studiedOnUtc;
    }
}

/// <summary>
/// Study modülünün kendi sınırı içinde tuttuğu öğrenci → kullanıcı bağı (sahiplik).
/// StudentId, M03 StudentProfile.Id'ye mantıksal referanstır; ilk yazımda oturum açan kullanıcıya bağlanır.
/// </summary>
public sealed class StudyStudent : Entity<Guid>
{
    private StudyStudent()
    {
    }

    public StudyStudent(Guid studentId, Guid userId, DateTime createdOnUtc)
    {
        Id = studentId;
        UserId = userId;
        CreatedOnUtc = createdOnUtc;
        UpdatedOnUtc = createdOnUtc;
    }

    public Guid UserId { get; private set; }

    /// <summary>Çalışma seansı verisi veliyle paylaşılsın mı (yeni kayıtların varsayılanı).</summary>
    public bool ShareStudyWithParent { get; private set; }

    /// <summary>Test/deneme verisi veliyle paylaşılsın mı.</summary>
    public bool ShareTestsWithParent { get; private set; }

    /// <summary>Çalışma seansı verisi bağlı öğretmenle paylaşılsın mı.</summary>
    public bool ShareStudyWithTeacher { get; private set; }

    /// <summary>Test/deneme verisi bağlı öğretmenle paylaşılsın mı.</summary>
    public bool ShareTestsWithTeacher { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime UpdatedOnUtc { get; private set; }

    public void UpdateSharing(
        bool shareStudyWithParent,
        bool shareTestsWithParent,
        bool shareStudyWithTeacher,
        bool shareTestsWithTeacher,
        DateTime nowUtc)
    {
        ShareStudyWithParent = shareStudyWithParent;
        ShareTestsWithParent = shareTestsWithParent;
        ShareStudyWithTeacher = shareStudyWithTeacher;
        ShareTestsWithTeacher = shareTestsWithTeacher;
        UpdatedOnUtc = nowUtc;
    }
}

public enum StudySessionStatus
{
    Running = 1,
    Paused = 2,
    Completed = 3,
    Discarded = 4
}

public enum StudySessionSource
{
    Stopwatch = 1,
    Manual = 2
}

public enum TestType
{
    Branch = 1,
    General = 2,
    Subject = 3,
    Topic = 4
}

public enum AchievementCategory
{
    Streak = 1,
    StudyTime = 2,
    TestPerformance = 3,
    Goal = 4,
    Consistency = 5
}

public sealed record StudySessionStartedDomainEvent(
    Guid SessionId,
    Guid StudentId,
    string Subject,
    DateTime StartedAtUtc) : DomainEvent;

public sealed record StudySessionCompletedDomainEvent(
    Guid SessionId,
    Guid StudentId,
    string Subject,
    string? Topic,
    int EffectiveMinutes,
    int BreakMinutes,
    DateTime EndedAtUtc) : DomainEvent;

public sealed record TestResultRecordedDomainEvent(
    Guid TestResultId,
    Guid StudentId,
    string Subject,
    string? Topic,
    int TotalQuestions,
    int Correct,
    int Wrong,
    int Blank,
    decimal Net,
    DateTime TakenOnUtc) : DomainEvent;

public sealed record StudyGoalUpdatedDomainEvent(
    Guid GoalId,
    Guid StudentId,
    int DailyGoalMinutes,
    DateTime UpdatedOnUtc) : DomainEvent;

public sealed record StreakMilestoneReachedDomainEvent(
    Guid StreakId,
    Guid StudentId,
    int CurrentStreakDays,
    int LongestStreakDays) : DomainEvent;

public sealed record StreakBrokenDomainEvent(
    Guid StreakId,
    Guid StudentId,
    int PreviousStreakDays) : DomainEvent;

public sealed record AchievementEarnedDomainEvent(
    Guid StudentAchievementId,
    Guid StudentId,
    string AchievementCode,
    DateTime EarnedOnUtc) : DomainEvent;
