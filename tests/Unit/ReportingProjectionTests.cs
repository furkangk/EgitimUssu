using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P08 Task 3 / M14-1, M14-2, M14-3, M14-5 — raporlama projeksiyonları.
/// <para>
/// Üç projeksiyon da <b>kümeden türetilen</b> sayaç kullanır (P07 Task 2 dersi): her ders/test/ödeme bir
/// satırdır, ekleme upsert'tir. Böylece aynı iş olayı iki kez yayınlanırsa (ders yeniden tamamlanır,
/// ödev iki farklı olayla kapanır) sayaç bozulmaz. Tekrarlanan <b>aynı</b> event'i inbox zaten eler.
/// </para>
/// </summary>
public sealed class ReportingProjectionTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 6, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TeacherUserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid StudentId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid OtherStudentId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    // --- Dönem kovaları ------------------------------------------------------------------------

    [Fact]
    public void Month_Bucket_Should_Follow_Utc_Not_Local_Time()
    {
        // 31 Ağustos 23:00 UTC → Ağustos; 1 Eylül 00:00 UTC → Eylül.
        Assert.Equal((2026, 8), ReportingPeriods.MonthOf(new DateTime(2026, 8, 31, 23, 0, 0, DateTimeKind.Utc)));
        Assert.Equal((2026, 9), ReportingPeriods.MonthOf(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Week_Bucket_Should_Use_Iso_Week_And_Cross_Year_Correctly()
    {
        // 2026-01-01 Perşembe → ISO hafta 1 / 2026.
        Assert.Equal((2026, 1), ReportingPeriods.WeekOf(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));

        // 2027-01-01 Cuma → ISO'da hâlâ 2026'nın 53. haftası (takvim yılı 2027 olsa da).
        Assert.Equal((2026, 53), ReportingPeriods.WeekOf(new DateTime(2027, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
    }

    // --- M14-1: öğretmen aylık ------------------------------------------------------------------

    [Fact]
    public void Teacher_Monthly_Should_Count_Lessons_And_Distinct_Students()
    {
        var projection = new TeacherMonthlyProjection(TeacherUserId, 2026, 9);

        projection.TrackCompletedLesson(Guid.NewGuid(), StudentId);
        projection.TrackCompletedLesson(Guid.NewGuid(), StudentId);
        projection.TrackCompletedLesson(Guid.NewGuid(), OtherStudentId);
        projection.TrackCancelledLesson(Guid.NewGuid());

        Assert.Equal(3, projection.CompletedLessonCount);
        Assert.Equal(2, projection.ActiveStudentCount);   // aynı öğrenci iki ders → bir kez sayılır
        Assert.Equal(1, projection.CancelledLessonCount);
    }

    [Fact]
    public void Teacher_Monthly_Should_Not_Double_Count_Same_Lesson()
    {
        var projection = new TeacherMonthlyProjection(TeacherUserId, 2026, 9);
        var lessonSessionId = Guid.NewGuid();

        projection.TrackCompletedLesson(lessonSessionId, StudentId);
        projection.TrackCompletedLesson(lessonSessionId, StudentId);   // ders yeniden tamamlandı

        Assert.Equal(1, projection.CompletedLessonCount);
    }

    [Fact]
    public void Teacher_Monthly_Should_Upsert_Expected_And_Accumulate_Collected()
    {
        var projection = new TeacherMonthlyProjection(TeacherUserId, 2026, 9);
        var paymentRecordId = Guid.NewGuid();

        projection.TrackExpectedPayment(paymentRecordId, 500m);
        projection.TrackExpectedPayment(paymentRecordId, 500m);   // aynı kayıt tekrar → tek satır
        projection.AddCollected(200m);
        projection.AddCollected(300m);                             // kısmi tahsilat farkları toplanır

        Assert.Equal(500m, projection.ExpectedAmount);
        Assert.Equal(500m, projection.CollectedAmount);
    }

    // --- M14-2: öğrenci haftalık çalışma --------------------------------------------------------

    [Fact]
    public void Student_Study_Should_Sum_Minutes_And_Count_Sessions_And_Tests()
    {
        var projection = new StudentStudyProjection(StudentId, 2026, 36);

        projection.TrackSession(Guid.NewGuid(), 45);
        projection.TrackSession(Guid.NewGuid(), 30);
        projection.TrackTest(Guid.NewGuid());

        Assert.Equal(75, projection.StudyMinutes);
        Assert.Equal(2, projection.SessionCount);
        Assert.Equal(1, projection.TestCount);
    }

    [Fact]
    public void Student_Study_Should_Update_Not_Duplicate_Same_Session()
    {
        var projection = new StudentStudyProjection(StudentId, 2026, 36);
        var sessionId = Guid.NewGuid();

        projection.TrackSession(sessionId, 45);
        projection.TrackSession(sessionId, 60);   // seans düzeltildi → süre güncellenir, satır çoğalmaz

        Assert.Equal(1, projection.SessionCount);
        Assert.Equal(60, projection.StudyMinutes);
    }

    // --- M14-3: öğrenci aylık performans --------------------------------------------------------

    [Fact]
    public void Student_Performance_Should_Average_Topics_And_Tests()
    {
        var projection = new StudentPerformanceProjection(StudentId, "Matematik", 2026, 9);

        projection.TrackTopicMastery("Türev", 40m);
        projection.TrackTopicMastery("İntegral", 60m);
        projection.TrackTestNet(Guid.NewGuid(), 0.40m);
        projection.TrackTestNet(Guid.NewGuid(), 0.60m);

        Assert.Equal(50, projection.MasteryScore);      // (40 + 60) / 2, tam sayıya yuvarlanır
        Assert.Equal(0.50m, projection.AverageNet);
        Assert.Equal(2, projection.TestCount);
    }

    [Fact]
    public void Student_Performance_Should_Keep_Latest_Score_Per_Topic()
    {
        var projection = new StudentPerformanceProjection(StudentId, "Matematik", 2026, 9);

        projection.TrackTopicMastery("Türev", 40m);
        projection.TrackTopicMastery("Türev", 80m);   // aynı konu yeniden değerlendirildi

        Assert.Equal(80, projection.MasteryScore);
    }

    [Fact]
    public void Performance_Delta_Should_Come_From_Previous_Month_Not_Storage()
    {
        // Öğrenilenler #102: komşuya bağlı değer saklanmaz, sorgu anında türetilir.
        Assert.Equal(0, ReportingDelta.FromScores(null, 55));
        Assert.Equal(15, ReportingDelta.FromScores(40, 55));
        Assert.Equal(-10, ReportingDelta.FromScores(50, 40));
    }

    // --- Handler'lar (event → projeksiyon) ------------------------------------------------------

    [Fact]
    public async Task Completed_Lesson_Event_Should_Land_In_Utc_Month_Bucket()
    {
        await using var dbContext = NewDbContext();
        var handler = new TeacherMonthlyLessonCompletedProjectionHandler(dbContext, new FixedClock(Now));

        await handler.HandleAsync(
            Envelope("LessonSessions", "LessonSessionCompletedDomainEvent", new
            {
                lessonSessionId = Guid.NewGuid(),
                lessonScheduleId = (Guid?)null,
                teacherUserId = TeacherUserId,
                studentId = StudentId,
                completedOnUtc = new DateTime(2026, 8, 31, 23, 30, 0, DateTimeKind.Utc)
            }),
            CancellationToken.None);

        var projection = await dbContext.TeacherMonthlyProjections.Include(p => p.Items).SingleAsync();
        Assert.Equal(2026, projection.Year);
        Assert.Equal(8, projection.Month);            // yerel saat değil UTC belirler
        Assert.Equal(1, projection.CompletedLessonCount);
    }

    [Fact]
    public async Task Same_Event_Twice_Should_Be_Deduplicated_By_Inbox()
    {
        await using var dbContext = NewDbContext();
        var handler = new TeacherMonthlyLessonCompletedProjectionHandler(dbContext, new FixedClock(Now));
        var envelope = Envelope("LessonSessions", "LessonSessionCompletedDomainEvent", new
        {
            lessonSessionId = Guid.NewGuid(),
            lessonScheduleId = (Guid?)null,
            teacherUserId = TeacherUserId,
            studentId = StudentId,
            completedOnUtc = Now
        });

        await handler.HandleAsync(envelope, CancellationToken.None);
        await handler.HandleAsync(envelope, CancellationToken.None);

        var projection = await dbContext.TeacherMonthlyProjections.Include(p => p.Items).SingleAsync();
        Assert.Equal(1, projection.CompletedLessonCount);
        Assert.Single(await dbContext.Set<InboxMessage>().ToListAsync());
    }

    [Fact]
    public async Task Payment_Collected_Event_Should_Accumulate_Delta_Only()
    {
        await using var dbContext = NewDbContext();
        var handler = new TeacherMonthlyPaymentCollectedProjectionHandler(dbContext, new FixedClock(Now));
        var paymentRecordId = Guid.NewGuid();

        await handler.HandleAsync(PaymentUpdated(paymentRecordId, 0m, 200m), CancellationToken.None);
        await handler.HandleAsync(PaymentUpdated(paymentRecordId, 200m, 500m), CancellationToken.None);

        var projection = await dbContext.TeacherMonthlyProjections.SingleAsync();
        Assert.Equal(500m, projection.CollectedAmount);   // 200 + 300, çift sayım yok
    }

    [Fact]
    public async Task Study_Session_Event_Should_Land_In_Iso_Week_Bucket()
    {
        await using var dbContext = NewDbContext();
        var handler = new StudentStudySessionProjectionHandler(dbContext, new FixedClock(Now));

        await handler.HandleAsync(
            Envelope("Study", "StudySessionCompletedDomainEvent", new
            {
                sessionId = Guid.NewGuid(),
                studentId = StudentId,
                subject = "Matematik",
                topic = (string?)null,
                effectiveMinutes = 50,
                breakMinutes = 10,
                endedAtUtc = new DateTime(2026, 9, 3, 18, 0, 0, DateTimeKind.Utc)
            }),
            CancellationToken.None);

        var projection = await dbContext.StudentStudyProjections.Include(p => p.Items).SingleAsync();
        Assert.Equal(2026, projection.Year);
        Assert.Equal(36, projection.WeekOfYear);
        Assert.Equal(50, projection.StudyMinutes);
        Assert.Equal(1, projection.SessionCount);
    }

    [Fact]
    public async Task Test_Result_Event_Should_Feed_Both_Study_And_Performance_Projections()
    {
        await using var dbContext = NewDbContext();
        var clock = new FixedClock(Now);
        var takenOn = new DateTime(2026, 9, 3, 18, 0, 0, DateTimeKind.Utc);
        var envelope = Envelope("Study", "TestResultRecordedDomainEvent", new
        {
            testResultId = Guid.NewGuid(),
            studentId = StudentId,
            subject = "Matematik",
            topic = "Türev",
            totalQuestions = 20,
            correct = 12,
            wrong = 0,
            blank = 8,
            net = 12m,
            takenOnUtc = takenOn
        });

        await new StudentStudyTestProjectionHandler(dbContext, clock).HandleAsync(envelope, CancellationToken.None);
        await new StudentPerformanceTestProjectionHandler(dbContext, clock).HandleAsync(envelope, CancellationToken.None);

        var study = await dbContext.StudentStudyProjections.Include(p => p.Items).SingleAsync();
        Assert.Equal(1, study.TestCount);

        var performance = await dbContext.StudentPerformanceProjections.Include(p => p.Items).SingleAsync();
        Assert.Equal("Matematik", performance.Subject);
        Assert.Equal(9, performance.Month);
        Assert.Equal(0.60m, performance.AverageNet);   // 12 / 20
    }

    [Fact]
    public async Task Topic_Mastery_Event_Should_Feed_Performance_Projection()
    {
        await using var dbContext = NewDbContext();
        var handler = new StudentPerformanceMasteryProjectionHandler(dbContext, new FixedClock(Now));

        await handler.HandleAsync(
            Envelope("ProgressTracking", "TopicMasteryChangedDomainEvent", new
            {
                topicMasteryId = Guid.NewGuid(),
                studentId = StudentId,
                subject = "Matematik",
                topic = "Türev",
                masteryLevel = "Developing",
                masteryScore = 42.5m,
                evaluatedOnUtc = new DateTime(2026, 9, 3, 18, 0, 0, DateTimeKind.Utc)
            }),
            CancellationToken.None);

        var projection = await dbContext.StudentPerformanceProjections.Include(p => p.Items).SingleAsync();
        Assert.Equal(43, projection.MasteryScore);   // 42.5 → 43
    }

    // --- M14-5: boş zaman analizi ---------------------------------------------------------------

    [Fact]
    public void Free_Slots_Should_Subtract_Lessons_From_Weekly_Availability()
    {
        // 2026-09-07 Pazartesi. Uygunluk 09:00–12:00; 10:00–11:00 ders dolu.
        var slots = new[] { new TeacherWeeklySlot(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)) };
        var busy = new[]
        {
            new TeacherBusyInterval(
                new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 7, 11, 0, 0, DateTimeKind.Utc),
                "Lesson")
        };

        var free = FreeSlotCalculator.Compute(
            slots, busy,
            new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, free.Count);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc), free[0].StartAtUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc), free[0].EndAtUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 11, 0, 0, DateTimeKind.Utc), free[1].StartAtUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc), free[1].EndAtUtc);
    }

    [Fact]
    public void Free_Slots_Should_Drop_Fragments_Shorter_Than_Minimum()
    {
        // 09:00–12:00 uygunluk, 09:20'ye kadar dolu → 20 dakikalık artık parça atılır (varsayılan eşik 30 dk).
        var slots = new[] { new TeacherWeeklySlot(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)) };
        var busy = new[]
        {
            new TeacherBusyInterval(
                new DateTime(2026, 9, 7, 9, 20, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc),
                "TimeOff")
        };

        var free = FreeSlotCalculator.Compute(
            slots, busy,
            new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(free);
    }

    [Fact]
    public void Free_Slots_Should_Merge_Overlapping_Busy_Intervals()
    {
        var slots = new[] { new TeacherWeeklySlot(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(15, 0)) };
        var busy = new[]
        {
            new TeacherBusyInterval(
                new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc), "Lesson"),
            new TeacherBusyInterval(
                new DateTime(2026, 9, 7, 11, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc), "TimeOff")
        };

        var free = FreeSlotCalculator.Compute(
            slots, busy,
            new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, free.Count);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc), free[0].StartAtUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc), free[0].EndAtUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc), free[1].StartAtUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 15, 0, 0, DateTimeKind.Utc), free[1].EndAtUtc);
    }

    [Fact]
    public void Free_Slots_Should_Repeat_Weekly_Slot_On_Every_Matching_Day()
    {
        var slots = new[] { new TeacherWeeklySlot(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0)) };

        var free = FreeSlotCalculator.Compute(
            slots, Array.Empty<TeacherBusyInterval>(),
            new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc));   // iki tam hafta

        Assert.Equal(2, free.Count);
        Assert.Equal(new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc), free[0].StartAtUtc);
        Assert.Equal(new DateTime(2026, 9, 14, 9, 0, 0, DateTimeKind.Utc), free[1].StartAtUtc);
    }

    // --- Yardımcılar ---------------------------------------------------------------------------

    private static IntegrationEvent PaymentUpdated(Guid paymentRecordId, decimal previous, decimal current)
        => Envelope("Payments", "PaymentRecordUpdatedDomainEvent", new
        {
            paymentRecordId,
            teacherUserId = TeacherUserId,
            studentId = StudentId,
            previousCollectedAmount = previous,
            currentCollectedAmount = current,
            updatedOnUtc = Now
        });

    private static IntegrationEvent Envelope(string sourceModule, string name, object payload)
        => new(
            Guid.NewGuid(),
            Now,
            name,
            sourceModule,
            System.Text.Json.JsonSerializer.Serialize(payload, IntegrationEventSerialization.Options));

    private static ReportingDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseInMemoryDatabase($"reporting-projection-{Guid.NewGuid():N}")
            .Options;
        return new ReportingDbContext(options, new NoOpDomainEventMapper());
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }
}
