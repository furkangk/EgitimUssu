using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealCompletionSyncTests(ContainerFixture fixture)
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_completion_updates_only_its_occurrence_and_blocks_cancellation_and_rescheduling(bool recurring)
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var infrastructure = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherId, studentId, scheduleId, anchor) = await SetupAsync(factory, "10.191.0.1", recurring);
        try
        {
            var key = recurring ? anchor.AddDays(7) : anchor;
            if (recurring) await JsonAsync(await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/reschedule", new
            {
                newStartAtUtc = key.AddHours(2), newEndAtUtc = key.AddHours(3), scope = 1, occurrenceStartAtUtc = key
            }));
            var session = await JsonAsync(await DeriveAsync(teacher, scheduleId, key));
            var sessionId = session.GetProperty("id").GetGuid();
            using var stranger = Client(factory, "10.191.0.2");
            await RegisterAsync(stranger, 2);
            var foreignCompletion = await stranger.PostAsJsonAsync($"/api/lesson-sessions/{sessionId}/complete", new
            {
                actualStartAtUtc = key, actualEndAtUtc = key.AddHours(1), attendanceStatus = 2, topicTitle = "Kesirler"
            });
            Assert.Equal(HttpStatusCode.Forbidden, foreignCompletion.StatusCode);
            await CompleteAsync(teacher, sessionId, key);
            await DrainAsync(factory);
            var cancel = await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/cancel", new
                { reason = 4, isChargeable = false, scope = 1, occurrenceStartAtUtc = key });
            Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
            Assert.Contains("scheduling.occurrence_completed", await cancel.Content.ReadAsStringAsync());
            var move = await teacher.PostAsJsonAsync($"/api/scheduling/lessons/{scheduleId}/reschedule", new
                { newStartAtUtc = key.AddHours(4), newEndAtUtc = key.AddHours(5), scope = 1, occurrenceStartAtUtc = key });
            Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
            var legacy = await teacher.PostAsync($"/api/scheduling/lessons/{scheduleId}/complete", null);
            Assert.True(legacy.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
            await CompleteAsync(teacher, sessionId, key);
            await DrainAsync(factory);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var lesson = await db.LessonSchedules.SingleAsync(item => item.Id == scheduleId);
            Assert.Equal(recurring ? LessonScheduleStatus.Planned : LessonScheduleStatus.Completed, lesson.Status);
            if (recurring)
            {
                var exception = await db.LessonOccurrenceExceptions.SingleAsync(item => item.SeriesLessonScheduleId == scheduleId);
                Assert.Equal(OccurrenceExceptionAction.Completed, exception.Action);
                Assert.Equal(key.AddHours(2), exception.OverrideStartAtUtc);
            }
            var list = await JsonAsync(await teacher.GetAsync($"/api/scheduling/teachers/{teacherId}/lessons?startAtUtc={anchor:O}&endAtUtc={anchor.AddDays(29):O}"));
            Assert.Equal("Completed", Assert.Single(list.EnumerateArray(), item => item.GetProperty("occurrenceStartAtUtc").GetDateTime() == key).GetProperty("status").GetString());
            if (recurring) Assert.Equal(4, list.EnumerateArray().Count(item => item.GetProperty("status").GetString() == "Planned"));
            Assert.False(await db.OutboxMessages.AnyAsync(item => item.Type == "LessonScheduleCancelledDomainEvent" && item.Payload.Contains(scheduleId.ToString())));
            var report = await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().TeacherMonthlyProjections.Include(item => item.Items).SingleAsync(item => item.TeacherUserId == teacherId);
            Assert.Equal(1, report.CompletedLessonCount);
            Assert.Equal(0, report.CancelledLessonCount);
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>().TopicMasteries.Where(item => item.StudentId == studentId).ExecuteDeleteAsync();
        }
    }

    private static async Task<(HttpClient Teacher, Guid TeacherId, Guid StudentId, Guid ScheduleId, DateTime Anchor)> SetupAsync(WebApplicationFactory<Program> factory, string ip, bool recurring)
    {
        var teacher = Client(factory, ip);
        var teacherId = await RegisterAsync(teacher, 2);
        var profile = await JsonAsync(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            createdByTeacherUserId = teacherId, fullName = "Oluşum Öğrencisi", gradeLevel = "8. Sınıf", origin = 1,
            subjects = Array.Empty<object>()
        }));
        var studentId = profile.GetProperty("id").GetGuid();
        var anchor = new DateTime(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);
        var lesson = await JsonAsync(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId = teacherId, studentId, subject = "Matematik", lessonFormat = 2,
            startAtUtc = anchor, endAtUtc = anchor.AddHours(1), timeZone = "Europe/Istanbul",
            recurrenceRule = recurring ? "FREQ=WEEKLY;BYDAY=MO;COUNT=5" : null, reminderOffsetMinutes = 60
        }));
        return (teacher, teacherId, studentId, lesson.GetProperty("id").GetGuid(), anchor);
    }

    private static Task<HttpResponseMessage> DeriveAsync(HttpClient teacher, Guid schedule, DateTime? occurrence)
        => teacher.PostAsJsonAsync("/api/lesson-sessions/from-schedule", new { lessonScheduleId = schedule, occurrenceStartAtUtc = occurrence });

    private static async Task CompleteAsync(HttpClient teacher, Guid session, DateTime start, int attendance = 2)
        => await JsonAsync(await teacher.PostAsJsonAsync($"/api/lesson-sessions/{session}/complete", new
        {
            actualStartAtUtc = start, actualEndAtUtc = start.AddHours(1), attendanceStatus = attendance,
            topicTitle = "Kesirler", teacherNotes = "Not", isChargeable = false
        }));

    private static HttpClient Client(WebApplicationFactory<Program> factory, string ip)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role)
    {
        var json = await JsonAsync(await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"weekly-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Oluşum", lastName = "Test", roles = new[] { role }
        }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return json.GetProperty("userId").GetGuid();
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.Clone();
    }

    private static async Task DrainAsync(WebApplicationFactory<Program> factory)
    {
        for (var round = 0; round < 30; round++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync() == 0) return;
        }
        Assert.Fail("Outbox boşalmadı.");
    }
}
