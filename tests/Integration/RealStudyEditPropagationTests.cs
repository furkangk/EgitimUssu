using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealStudyEditPropagationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Http_edits_deletes_mock_total_and_source_rebuild_propagate_through_real_outbox()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = await StudentClient(factory);
        var student = Guid.NewGuid();
        try
        {
            var now = factory.Services.GetRequiredService<IClock>().UtcNow.AddMinutes(-5);
            var exam = await Json(await client.PostAsJsonAsync($"/api/study/students/{student}/mock-exams", new
            {
                examType = "TYT", takenOnUtc = now.AddDays(-40), subjects = new[]
                {
                    new { subject = "Mat", topic = "Türev", totalQuestions = 20, correct = 16, wrong = 0, blank = 4 },
                    new { subject = "Fizik", topic = "Optik", totalQuestions = 20, correct = 10, wrong = 0, blank = 10 }
                }
            }));
            var examId = exam.GetProperty("id").GetGuid();
            var testId = exam.GetProperty("subjects")[0].GetProperty("id").GetGuid();
            var secondId = exam.GetProperty("subjects")[1].GetProperty("id").GetGuid();
            var session = await Json(await client.PostAsJsonAsync("/api/study/sessions/manual", new
            { studentId = student, subject = "Mat", topic = "Türev", effectiveMinutes = 60, studiedOnUtc = now }));
            var sessionId = session.GetProperty("id").GetGuid();
            await Drain(factory);
            var initial = await Snapshot(factory, student);
            Assert.Equal((60, 1, 2, 2, 26m), (initial.Minutes, initial.Sessions, initial.Tests, initial.MasteryTests, initial.MockTotal));
            Assert.Equal(60, initial.MasteryMinutes);

            await Json(await client.PutAsJsonAsync($"/api/study/test-results/{testId}", TestBody("Fizik", "Optik", now, 4)));
            await Json(await client.PutAsJsonAsync($"/api/study/sessions/{sessionId}", new
                { subject = "Fizik", topic = "Optik", effectiveMinutes = 20 }));
            await Drain(factory);
            var edited = await Snapshot(factory, student);
            Assert.Equal((20, 2, 14m), (edited.Minutes, edited.Tests, edited.MockTotal));
            Assert.Equal(20, edited.MasteryMinutes);
            await using (var read = factory.Services.CreateAsyncScope())
            {
                var db = read.ServiceProvider.GetRequiredService<ReportingDbContext>();
                var months = await db.StudentPerformanceProjections.Include(x => x.Items).Where(x => x.StudentId == student).ToArrayAsync();
                Assert.All(months.Where(x => x.Subject == "Mat"), x => Assert.Equal(0, x.TestCount));
                var currentMonth = ReportingMonth(now, factory.Services.GetRequiredService<ILocalCalendar>());
                Assert.Equal(.2m, Assert.Single(months, x => x.Subject == "Fizik" && x.Year == currentMonth.Year && x.Month == currentMonth.Month).AverageNet);
                var progress = read.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
                Assert.Equal(0, (await progress.TopicMasteries.SingleAsync(x => x.StudentId == student && x.Subject == "Mat")).TestAttemptCount);
            }
            Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/study/test-results/{testId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/study/sessions/{sessionId}")).StatusCode);
            await Drain(factory);
            var final = await Snapshot(factory, student);
            Assert.Equal((0, 0, 1, 1, 10m), (final.Minutes, final.Sessions, final.Tests, final.MasteryTests, final.MockTotal));

            IntegrationEvent[] stream;
            await using (var reset = factory.Services.CreateAsyncScope())
            {
                var db = reset.ServiceProvider.GetRequiredService<StudyDbContext>();
                var rows = await db.OutboxMessages.Where(x => x.Payload.Contains(student.ToString()) &&
                    (x.Type.Contains("StudySession") || x.Type.Contains("TestResult"))).OrderBy(x => x.OccurredOnUtc).ToArrayAsync();
                Assert.Contains(rows, x => x.Type == "StudySessionEditedDomainEvent");
                Assert.Contains(rows, x => x.Type == "StudySessionDeletedDomainEvent");
                Assert.Contains(rows, x => x.Type == "TestResultEditedDomainEvent");
                Assert.Contains(rows, x => x.Type == "TestResultDeletedDomainEvent");
                stream = rows.Select(x => JsonSerializer.Deserialize<IntegrationEvent>(x.Payload, IntegrationEventSerialization.Options)!).ToArray();
                Assert.All(rows, x => Assert.NotNull(x.ProcessedOnUtc));
                var reporting = reset.ServiceProvider.GetRequiredService<ReportingDbContext>();
                await reporting.StudentStudyProjections.Where(x => x.StudentId == student).ExecuteDeleteAsync();
                await reporting.StudentPerformanceProjections.Where(x => x.StudentId == student).ExecuteDeleteAsync();
                var progress = reset.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
                await progress.TopicMasteries.Where(x => x.StudentId == student).ExecuteDeleteAsync();
                var ids = stream.Select(x => x.EventId).ToArray();
                await progress.InboxMessages.Where(x => ids.Contains(x.EventId)).ExecuteDeleteAsync();
                await reporting.InboxMessages.Where(x => ids.Contains(x.EventId)).ExecuteDeleteAsync();
            }
            // Silme önce, eski ekleme en son: kaynakta kalan tek test yeniden kurulur.
            foreach (var message in stream.AsEnumerable().Reverse())
            {
                await using var replay = factory.Services.CreateAsyncScope();
                var bus = replay.ServiceProvider.GetRequiredService<IEventBus>();
                await bus.PublishAsync(message); await bus.PublishAsync(message);
                await bus.PublishAsync(message with { EventId = Guid.NewGuid() });
            }
            await Drain(factory);
            Assert.Equal(final, await Snapshot(factory, student));
            Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/study/test-results/{secondId}")).StatusCode);
            await Drain(factory);
            Assert.Equal(0m, (await Snapshot(factory, student)).MockTotal);
        }
        finally { await Cleanup(factory, student); }
    }

    [SkippableFact]
    public async Task Backdated_test_does_not_reverse_trend_and_concurrent_mock_edits_keep_total()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = await StudentClient(factory);
        var student = Guid.NewGuid();
        try
        {
            var now = factory.Services.GetRequiredService<IClock>().UtcNow.AddMinutes(-5);
            foreach (var (days, score) in new[] { (-2, 10), (-1, 18), (-3, 4) })
                await Json(await client.PostAsJsonAsync("/api/study/test-results", new
                { studentId = student, subject = "Mat", topic = "Türev", testType = "Subject", totalQuestions = 20,
                    correct = score, wrong = 0, blank = 20-score, penaltyDivisor = 4, takenOnUtc = now.AddDays(days) }));
            await Drain(factory);
            await using (var read = factory.Services.CreateAsyncScope())
            {
                var mastery = await read.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>().TopicMasteries
                    .SingleAsync(x => x.StudentId == student);
                Assert.Equal(ProgressTrend.Improving, mastery.Trend);
                Assert.Equal(.9m, mastery.RecentNetRatio); Assert.Equal(.5m, mastery.PriorNetRatio);
                Assert.Equal(3, mastery.TestAttemptCount);
            }
            var exam = await Json(await client.PostAsJsonAsync($"/api/study/students/{student}/mock-exams", new
            {
                examType = "TYT", takenOnUtc = now, subjects = new[]
                {
                    new { subject = "Fizik", topic = "Optik", totalQuestions = 20, correct = 16, wrong = 0, blank = 4 },
                    new { subject = "Kimya", topic = "Atom", totalQuestions = 20, correct = 10, wrong = 0, blank = 10 }
                }
            }));
            var first = exam.GetProperty("subjects")[0].GetProperty("id").GetGuid();
            var second = exam.GetProperty("subjects")[1].GetProperty("id").GetGuid();
            var writes = await Task.WhenAll(client.PutAsJsonAsync($"/api/study/test-results/{first}", TestBody("Fizik", "Optik", now, 4)),
                client.PutAsJsonAsync($"/api/study/test-results/{second}", TestBody("Kimya", "Atom", now, 8)));
            foreach (var response in writes) await Json(response);
            Assert.Equal(12m, (await Snapshot(factory, student)).MockTotal);
        }
        finally { await Cleanup(factory, student); }
    }

    [SkippableTheory]
    [InlineData(4)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Foreign_roles_cannot_edit_or_delete_another_students_records(int role)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var owner = await StudentClient(factory); using var foreign = await StudentClient(factory, role);
        var student = Guid.NewGuid();
        try
        {
            var now = factory.Services.GetRequiredService<IClock>().UtcNow.AddMinutes(-5);
            var session = await Json(await owner.PostAsJsonAsync("/api/study/sessions/manual", new
                { studentId = student, subject = "Mat", effectiveMinutes = 60, studiedOnUtc = now }));
            var test = await Json(await owner.PostAsJsonAsync("/api/study/test-results", new
                { studentId = student, subject = "Mat", testType = "Subject", totalQuestions = 20, correct = 16,
                    wrong = 0, blank = 4, takenOnUtc = now }));
            var id = test.GetProperty("id").GetGuid(); var sid = session.GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Forbidden, (await foreign.PutAsJsonAsync($"/api/study/test-results/{id}", TestBody("Mat", null, now, 4))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await foreign.DeleteAsync($"/api/study/test-results/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await foreign.PutAsJsonAsync($"/api/study/sessions/{sid}", new { subject = "Mat", effectiveMinutes = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await foreign.DeleteAsync($"/api/study/sessions/{sid}")).StatusCode);
        }
        finally { await Cleanup(factory, student); }
    }

    private static object TestBody(string subject, string? topic, DateTime at, int correct)
        => new { subject, topic, testType = "Subject", totalQuestions = 20, correct, wrong = 0, blank = 20-correct, penaltyDivisor = 4, takenOnUtc = at };
    private static (int Year, int Month) ReportingMonth(DateTime utc, ILocalCalendar calendar)
        => EgitimUssu.Modules.Reporting.Domain.ReportingPeriods.MonthOf(utc, calendar);
    private static async Task<HttpClient> StudentClient(WebApplicationFactory<Program> factory, int role = 3)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.198.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}");
        var auth = await Json(await client.PostAsJsonAsync("/api/identity/register", new
            { email = $"edit-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Edit", lastName = "Test", roles = new[] { role } }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task Drain(WebApplicationFactory<Program> factory)
    {
        for (var i = 0; i < 20; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync() == 0) return;
        }
        Assert.Fail("Outbox 20 turda boşalmadı.");
    }
    private sealed record Stats(int Minutes, int Sessions, int Tests, int MasteryTests, int MasteryMinutes, decimal MockTotal, decimal Score, string Nets);
    private static async Task<Stats> Snapshot(WebApplicationFactory<Program> factory, Guid student)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var study = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        var progress = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
        var reporting = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var weeks = await reporting.StudentStudyProjections.Include(x => x.Items).Where(x => x.StudentId == student).ToArrayAsync();
        var topics = await progress.TopicMasteries.Where(x => x.StudentId == student).ToArrayAsync();
        var months = await reporting.StudentPerformanceProjections.Include(x => x.Items).Where(x => x.StudentId == student).ToArrayAsync();
        return new(weeks.Sum(x => x.StudyMinutes), weeks.Sum(x => x.SessionCount), weeks.Sum(x => x.TestCount), topics.Sum(x => x.TestAttemptCount),
            topics.Sum(x => x.TotalStudyMinutes), await study.MockExams.Where(x => x.StudentId == student).SumAsync(x => x.TotalNet),
            topics.Sum(x => x.MasteryScore), string.Join(";", months.Where(x => x.TestCount > 0).OrderBy(x => x.Subject).ThenBy(x => x.Year).ThenBy(x => x.Month)
                .Select(x => $"{x.Subject}/{x.Year}/{x.Month}:{x.AverageNet}:{x.TestCount}")));
    }
    private static async Task Cleanup(WebApplicationFactory<Program> factory, Guid student)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var study = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
        await study.OutboxMessages.Where(x => x.Payload.Contains(student.ToString())).ExecuteDeleteAsync();
        var progress = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
        await progress.OutboxMessages.Where(x => x.Payload.Contains(student.ToString())).ExecuteDeleteAsync();
        await progress.TopicMasteries.Where(x => x.StudentId == student).ExecuteDeleteAsync();
        await progress.ProgressSnapshots.Where(x => x.StudentId == student).ExecuteDeleteAsync();
        var reporting = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await reporting.StudentStudyProjections.Where(x => x.StudentId == student).ExecuteDeleteAsync();
        await reporting.StudentPerformanceProjections.Where(x => x.StudentId == student).ExecuteDeleteAsync();
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
            using var json = JsonDocument.Parse(body); return json.RootElement.Clone();
        }
    }
}
