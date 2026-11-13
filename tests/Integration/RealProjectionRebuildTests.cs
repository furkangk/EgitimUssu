using EgitimUssu.Modules.Reporting.Application;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P08 Task 6 / K-04 kabul kriteri: <b>her projeksiyon yeniden kurulabilir olmalı.</b>
/// <para>
/// Burada kanıtlanan şey, `IProjectionRebuilder`'ın <i>uygulaması</i> değil (o host seviyesindedir,
/// Öğrenilenler #93 → P12), o uygulamanın dayandığı **özellik**tir: M14 projeksiyonları aynı event
/// akışının saf bir katlaması (fold) mıdır? Üç faz:
/// </para>
/// <list type="number">
///   <item>Olay akışı işlenir → sayaçlar okunur.</item>
///   <item>Projeksiyon tabloları <b>ve</b> Reporting inbox satırları boşaltılır, <b>aynı</b> akış
///         (aynı <c>EventId</c>'lerle) yeniden işlenir → sayaçlar birebir aynı çıkmalı.</item>
///   <item>Akış <b>üçüncü</b> kez, bu kez inbox boşaltılmadan işlenir → hiçbir sayaç değişmemeli
///         (en-az-bir-kez teslimde çift sayım yok).</item>
/// </list>
/// <para>
/// Bu, gerçek Postgres'te koşmak zorunda: <c>ExecuteDelete</c> + FK cascade davranışı ve bileşik
/// anahtarlı inbox guard'ı InMemory sağlayıcıda kanıtlanamaz.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealProjectionRebuildTests(ContainerFixture fixture)
{
    private static readonly DateTime September = new(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc);

    [SkippableFact]
    public async Task Reporting_Projections_Should_Rebuild_To_Identical_Values_From_The_Same_Event_Stream()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacherUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var stream = BuildEventStream(teacherUserId, studentId);

        // --- Faz 1: ilk besleme -----------------------------------------------------------------
        await PublishAsync(factory, stream);
        var initial = await ReadSnapshotAsync(factory, teacherUserId, studentId);

        // Akış gerçekten bir şey yazmış olmalı; boş bir snapshot'ı "aynı" bulmak yalancı-yeşil olurdu.
        Assert.Equal(2, initial.CompletedLessons);
        Assert.Equal(1, initial.CancelledLessons);
        Assert.Equal(1, initial.ActiveStudents);
        Assert.Equal(750m, initial.ExpectedAmount);
        Assert.Equal(250m, initial.CollectedAmount);
        Assert.Equal(95, initial.StudyMinutes);
        Assert.Equal(1, initial.SessionCount);
        Assert.Equal(1, initial.TestCount);
        Assert.Equal("Matematik:70:0,7500:1", initial.Performance);

        // --- Faz 2: yeniden inşa ----------------------------------------------------------------
        await ResetProjectionStoreAsync(factory);
        var cleared = await ReadSnapshotAsync(factory, teacherUserId, studentId);
        Assert.Equal(ProjectionSnapshot.Empty, cleared);

        await PublishAsync(factory, stream);   // AYNI olaylar, AYNI EventId'ler
        var rebuilt = await ReadSnapshotAsync(factory, teacherUserId, studentId);
        Assert.Equal(initial, rebuilt);

        // --- Faz 3: tekrar teslim (inbox boşaltılmadan) -----------------------------------------
        await PublishAsync(factory, stream);
        var redelivered = await ReadSnapshotAsync(factory, teacherUserId, studentId);
        Assert.Equal(initial, redelivered);
    }

    /// <summary>
    /// M14'ün beslendiği beş kaynak olayı, <b>sabit</b> <c>EventId</c>'lerle. Sabitlik şart:
    /// yeniden inşa "aynı akışı tekrar oynatmak"tır, yeni kimliklerle üretilen akış değil.
    /// </summary>
    private static IReadOnlyList<IntegrationEvent> BuildEventStream(Guid teacherUserId, Guid studentId)
    {
        var lessonOne = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var lessonTwo = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var cancelled = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var payment = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var session = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var testResult = Guid.Parse("66666666-6666-6666-6666-666666666666");

        return new[]
        {
            Envelope("A0000001-0000-0000-0000-000000000001", "LessonSessions", "LessonSessionCompletedDomainEvent", new
            {
                lessonSessionId = lessonOne,
                teacherUserId,
                studentId,
                completedOnUtc = September,
            }),
            Envelope("A0000001-0000-0000-0000-000000000002", "LessonSessions", "LessonSessionCompletedDomainEvent", new
            {
                lessonSessionId = lessonTwo,
                teacherUserId,
                studentId,
                completedOnUtc = September,
            }),
            Envelope("A0000001-0000-0000-0000-000000000003", "Scheduling", "LessonScheduleCancelledDomainEvent", new
            {
                lessonScheduleId = cancelled,
                teacherUserId,
                cancelledOnUtc = September,
            }),
            Envelope("A0000001-0000-0000-0000-000000000004", "Payments", "PaymentRecordCreatedDomainEvent", new
            {
                paymentRecordId = payment,
                teacherUserId,
                expectedAmount = 750m,
                createdOnUtc = September,
            }),
            Envelope("A0000001-0000-0000-0000-000000000005", "Payments", "PaymentRecordUpdatedDomainEvent", new
            {
                paymentRecordId = payment,
                teacherUserId,
                previousCollectedAmount = 0m,
                currentCollectedAmount = 250m,
                updatedOnUtc = September,
            }),
            Envelope("A0000001-0000-0000-0000-000000000006", "Study", "StudySessionCompletedDomainEvent", new
            {
                sessionId = session,
                studentId,
                effectiveMinutes = 95,
                endedAtUtc = September,
            }),
            Envelope("A0000001-0000-0000-0000-000000000007", "Study", "TestResultRecordedDomainEvent", new
            {
                testResultId = testResult,
                studentId,
                subject = "Matematik",
                totalQuestions = 40,
                net = 30m,
                takenOnUtc = September,
            }),
            Envelope("A0000001-0000-0000-0000-000000000008", "ProgressTracking", "TopicMasteryChangedDomainEvent", new
            {
                studentId,
                subject = "Matematik",
                topic = "Türev",
                masteryScore = 70m,
                evaluatedOnUtc = September,
            }),
        };
    }

    private static IntegrationEvent Envelope(string eventId, string sourceModule, string name, object payload)
        => new(
            Guid.Parse(eventId),
            September,
            name,
            sourceModule,
            System.Text.Json.JsonSerializer.Serialize(payload, IntegrationEventSerialization.Options));

    /// <summary>
    /// Akışı <b>yalnız Reporting'in</b> handler'larına verir; genel <c>IEventBus</c> kullanılmaz.
    /// <para>
    /// Bu bilinçli: bir projeksiyon yeniden inşası <b>tek bir projeksiyon deposunu</b> hedefler.
    /// Rebuild sırasında genel bus'a yayın yapmak diğer modüllerin tüketicilerini de yeniden
    /// tetiklerdi (ör. M05 ders tamamlanma olayı ProgressTracking'de konu katılımı yazar) — yani
    /// rebuild, yeniden kurmayı amaçlamadığı verileri bozardı. Testin komşu testleri kirletmesi
    /// de aynı hatanın belirtisiydi.
    /// </para>
    /// Her olay için ayrı scope açılır — <c>DispatchingEventBus</c>'ın üretimdeki davranışının aynısı.
    /// </summary>
    private static async Task PublishAsync(
        WebApplicationFactory<Program> factory, IReadOnlyList<IntegrationEvent> stream)
    {
        var reportingAssembly = typeof(ReportingDbContext).Assembly;

        foreach (var integrationEvent in stream)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var handlers = scope.ServiceProvider
                .GetServices<IIntegrationEventHandler>()
                .Where(handler => handler.GetType().Assembly == reportingAssembly)
                .Where(handler => handler.CanHandle(integrationEvent))
                .ToArray();

            foreach (var handler in handlers)
            {
                await handler.HandleAsync(integrationEvent, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Yeniden inşanın ilk adımı: projeksiyon satırları <b>ve</b> onları "işlenmiş" sayan inbox
    /// satırları silinir. İnbox bırakılırsa tekrar oynatma sessizce boş bir projeksiyon üretir —
    /// bir rebuild uygulamasının atlamaması gereken tuzak budur.
    /// </summary>
    private static async Task ResetProjectionStoreAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        // Alt satırlar (items) FK'de ON DELETE CASCADE ile bağlı; üst satırın silinmesi onları da götürür.
        await dbContext.TeacherDashboardProjections.ExecuteDeleteAsync();
        await dbContext.TeacherMonthlyProjections.ExecuteDeleteAsync();
        await dbContext.StudentStudyProjections.ExecuteDeleteAsync();
        await dbContext.StudentPerformanceProjections.ExecuteDeleteAsync();
        await dbContext.InboxMessages.ExecuteDeleteAsync();
    }

    private static async Task<ProjectionSnapshot> ReadSnapshotAsync(
        WebApplicationFactory<Program> factory, Guid teacherUserId, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IReportingProjectionReader>();

        var monthly = await reader.GetTeacherMonthlyAsync(teacherUserId, 2026, 9, CancellationToken.None);
        var study = await reader.ListStudentStudyAsync(studentId, null, null, CancellationToken.None);
        var performance = await reader.ListStudentPerformanceAsync(studentId, null, null, null, CancellationToken.None);

        return new ProjectionSnapshot(
            monthly?.CompletedLessonCount ?? 0,
            monthly?.CancelledLessonCount ?? 0,
            monthly?.ActiveStudentCount ?? 0,
            monthly?.ExpectedAmount ?? 0m,
            monthly?.CollectedAmount ?? 0m,
            study.Sum(item => item.StudyMinutes),
            study.Sum(item => item.SessionCount),
            study.Sum(item => item.TestCount),
            string.Join(
                "|",
                performance
                    .OrderBy(item => item.Subject, StringComparer.Ordinal)
                    .ThenBy(item => item.Year)
                    .ThenBy(item => item.Month)
                    .Select(item => $"{item.Subject}:{item.MasteryScore}:{item.AverageNet}:{item.TestCount}")));
    }

    private sealed record ProjectionSnapshot(
        int CompletedLessons,
        int CancelledLessons,
        int ActiveStudents,
        decimal ExpectedAmount,
        decimal CollectedAmount,
        int StudyMinutes,
        int SessionCount,
        int TestCount,
        string Performance)
    {
        public static readonly ProjectionSnapshot Empty = new(0, 0, 0, 0m, 0m, 0, 0, 0, string.Empty);
    }
}
