using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P16 T4 / N-07 — gerçek Postgres + gerçek HTTP.
/// <para>
/// Çakışma kontrolü eskiden yalnız serinin ilk satırına bakıyordu, tatil bloğunu görmüyordu ve "kontrol et → ekle"
/// iki ayrı adımdı: aynı saate iki eşzamanlı POST ikisi de geçiyordu. Artık oluşumlar genişletilir, tatil bloğu
/// <c>409 scheduling.time_off_conflict</c> döner (bilerek <c>allowTimeOffOverride</c>), ve öğretmenin takvimine yazan her
/// iş öğretmen başına <c>pg_advisory_xact_lock(hashtext('sched:' || teacherId))</c> altında kontrol + yazım + kaydetmeyi
/// tek transaction'da yapar.
/// </para>
/// <para>
/// Paralel POST testi tek başına ayırt edici değildir (istekler sırayla koşarsa genişletilmiş kontrol de tek kazanan
/// verir); kilidin gerçekten alındığını <see cref="Writes_Wait_For_The_Teachers_Schedule_Lock_And_Other_Teachers_Do_Not"/>
/// kanıtlar: test aynı anahtarı kendi transaction'ında tutar ve yazımların beklediğini görür.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealConcurrentLessonCreateTests(ContainerFixture fixture)
{
    private static readonly TimeSpan BlockedProbe = TimeSpan.FromMilliseconds(1500);

    [SkippableFact]
    public async Task Parallel_Creates_For_The_Same_Slot_Have_Exactly_One_Winner()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, studentId) = await CreateTeacherWithStudentAsync(factory, "10.181.0.1", "Paralel");

        for (var round = 0; round < 3; round++)
        {
            var slot = FutureTuesday(weeksAhead: 3 + round);
            using var gate = new SemaphoreSlim(0);
            var racers = Enumerable.Range(0, 6).Select(async _ =>
            {
                await gate.WaitAsync();
                return await CreateLessonAsync(teacher, teacherUserId, studentId, slot);
            }).ToArray();
            gate.Release(racers.Length);
            var responses = await Task.WhenAll(racers);

            var statuses = responses.Select(response => (int)response.StatusCode).OrderBy(code => code).ToArray();
            Assert.Equal(new[] { 200, 409, 409, 409, 409, 409 }, statuses);
            foreach (var rejected in responses.Where(response => response.StatusCode == HttpStatusCode.Conflict))
            {
                Assert.Equal("scheduling.teacher_conflict", await ReadCodeAsync(rejected));
            }

            Assert.Equal(1, await CountLessonsAsync(factory, teacherUserId, slot));
        }
    }

    [SkippableFact]
    public async Task Writes_Wait_For_The_Teachers_Schedule_Lock_And_Other_Teachers_Do_Not()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, studentId) = await CreateTeacherWithStudentAsync(factory, "10.181.1.1", "Kilit");
        var (other, otherUserId, otherStudentId) = await CreateTeacherWithStudentAsync(factory, "10.181.1.2", "KilitDiger");

        var anchor = FutureTuesday(weeksAhead: 2);
        var seriesId = await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, anchor, "FREQ=WEEKLY;BYDAY=TU"));

        // Testin transaction'ı öğretmenin takvim anahtarını tutarken öğretmenin üç yazımı (oluştur, tek oluşumu ertele,
        // tek oluşumu iptal et — #223 g) beklemeli; başka öğretmenin yazımı beklememeli.
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@key))", connection, transaction))
        {
            hold.Parameters.AddWithValue("key", $"sched:{teacherUserId}");
            await hold.ExecuteNonQueryAsync();
        }

        var create = CreateLessonAsync(teacher, teacherUserId, studentId, anchor.AddDays(2));
        var reschedule = teacher.PostAsJsonAsync($"/api/scheduling/lessons/{seriesId}/reschedule", new
        {
            newStartAtUtc = anchor.AddHours(3),
            newEndAtUtc = anchor.AddHours(4),
            note = "Kilitte",
            scope = 1, // OccurrenceScope.Single
            occurrenceStartAtUtc = anchor,
        });
        var cancel = teacher.PostAsJsonAsync($"/api/scheduling/lessons/{seriesId}/cancel", new
        {
            reason = 1,
            isChargeable = false,
            cancellationNote = "Kilitte",
            scope = 1, // OccurrenceScope.Single
            occurrenceStartAtUtc = anchor.AddDays(7),
        });

        var unrelated = await CreateLessonAsync(other, otherUserId, otherStudentId, anchor.AddDays(2));
        Assert.Equal(HttpStatusCode.OK, unrelated.StatusCode);

        await Task.Delay(BlockedProbe);
        Assert.False(create.IsCompleted, "Oluşturma öğretmen kilidini beklemeliydi.");
        Assert.False(reschedule.IsCompleted, "Tek oluşum ertelemesi öğretmen kilidini beklemeliydi.");
        Assert.False(cancel.IsCompleted, "Tek oluşum iptali öğretmen kilidini beklemeliydi.");

        await transaction.CommitAsync();

        foreach (var response in await Task.WhenAll(create, reschedule, cancel))
        {
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    [SkippableFact]
    public async Task Weekly_Series_Started_Weeks_Ago_Blocks_A_Lesson_On_A_Later_Tuesday()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, studentId) = await CreateTeacherWithStudentAsync(factory, "10.181.2.1", "Seri");

        var laterTuesday = FutureTuesday(weeksAhead: 3);
        var seriesAnchor = laterTuesday.AddDays(-7 * 10); // ~10 hafta önce başlamış haftalık Salı 17:00 TR
        Assert.Equal(HttpStatusCode.OK, (await CreateLessonAsync(teacher, teacherUserId, studentId, seriesAnchor, "FREQ=WEEKLY;BYDAY=TU")).StatusCode);

        var sameTime = await CreateLessonAsync(teacher, teacherUserId, studentId, laterTuesday);
        Assert.Equal(HttpStatusCode.Conflict, sameTime.StatusCode);
        Assert.Equal("scheduling.teacher_conflict", await ReadCodeAsync(sameTime));

        // Oluşum 14:00–15:00Z; 14:30 adayı oluşumdan sonra başlar ama içine düşer.
        Assert.Equal(HttpStatusCode.Conflict, (await CreateLessonAsync(teacher, teacherUserId, studentId, laterTuesday.AddMinutes(30))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CreateLessonAsync(teacher, teacherUserId, studentId, laterTuesday.AddHours(1))).StatusCode);
    }

    [SkippableFact]
    public async Task Time_Off_Block_Rejects_Lessons_And_Reschedules_Unless_Overridden()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, studentId) = await CreateTeacherWithStudentAsync(factory, "10.181.3.1", "Tatil");

        var tuesday = FutureTuesday(weeksAhead: 2);
        var blockStart = tuesday.Date.AddHours(-3); // Salı 00:00 TR
        Assert.Equal(HttpStatusCode.OK, (await CreateTimeOffAsync(teacher, teacherUserId, blockStart, blockStart.AddDays(1))).StatusCode);

        var inside = await CreateLessonAsync(teacher, teacherUserId, studentId, tuesday);
        Assert.Equal(HttpStatusCode.Conflict, inside.StatusCode);
        Assert.Equal("scheduling.time_off_conflict", await ReadCodeAsync(inside));
        Assert.Equal(0, await CountLessonsAsync(factory, teacherUserId, tuesday));

        Assert.Equal(HttpStatusCode.OK, (await CreateLessonAsync(teacher, teacherUserId, studentId, tuesday, allowTimeOffOverride: true)).StatusCode);

        // Bloğun dışındaki bir dersi bloğa ertelemek de bilerek yapılmalı.
        var lessonId = await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, tuesday.AddDays(1)));
        var moved = await RescheduleAsync(teacher, lessonId, tuesday.AddHours(2), allowTimeOffOverride: false);
        Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        Assert.Equal("scheduling.time_off_conflict", await ReadCodeAsync(moved));
        Assert.Equal(HttpStatusCode.OK, (await RescheduleAsync(teacher, lessonId, tuesday.AddHours(2), allowTimeOffOverride: true)).StatusCode);
    }

    [SkippableFact]
    public async Task Creating_Time_Off_Lists_A_Lesson_Spilling_Into_The_Block_And_The_Series_Occurrence()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var (teacher, teacherUserId, studentId) = await CreateTeacherWithStudentAsync(factory, "10.181.4.1", "TatilListe");

        var tuesday = FutureTuesday(weeksAhead: 2);
        var blockStart = tuesday.AddHours(-5); // 09:00Z
        var spillingStart = blockStart.AddHours(-1);
        var spillingId = await ReadIdAsync(await CreateLessonAsync(
            teacher, teacherUserId, studentId, spillingStart, duration: TimeSpan.FromHours(2)));
        var seriesId = await ReadIdAsync(await CreateLessonAsync(
            teacher, teacherUserId, studentId, tuesday.AddDays(-7 * 6), "FREQ=WEEKLY;BYDAY=TU"));

        var response = await CreateTimeOffAsync(teacher, teacherUserId, blockStart, blockStart.AddHours(12));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        var listed = document.RootElement.GetProperty("conflictingLessons").EnumerateArray()
            .Select(item => (
                Id: item.GetProperty("id").GetGuid(),
                Start: item.GetProperty("startAtUtc").GetDateTime().ToUniversalTime(),
                Occurrence: item.GetProperty("occurrenceStartAtUtc").GetDateTime().ToUniversalTime()))
            .ToArray();
        Assert.Equal(new[] { spillingId, seriesId }, listed.Select(item => item.Id));
        Assert.Equal(spillingStart, listed[0].Start);
        Assert.Equal(tuesday, listed[1].Start);
        Assert.Equal(tuesday, listed[1].Occurrence);
    }

    /// <summary>En az <paramref name="weeksAhead"/> hafta sonraki Salı 14:00Z (17:00 TR).</summary>
    private static DateTime FutureTuesday(int weeksAhead)
    {
        var day = DateTime.UtcNow.Date.AddDays(7 * weeksAhead);
        while (day.DayOfWeek != DayOfWeek.Tuesday)
        {
            day = day.AddDays(1);
        }

        return DateTime.SpecifyKind(day.AddHours(14), DateTimeKind.Utc);
    }

    private static Task<HttpResponseMessage> CreateLessonAsync(
        HttpClient teacher, Guid teacherUserId, Guid studentId, DateTime startAtUtc,
        string? recurrenceRule = null, bool allowTimeOffOverride = false, TimeSpan? duration = null)
        => teacher.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId,
            studentId,
            subject = "Matematik",
            lessonFormat = 2,
            startAtUtc,
            endAtUtc = startAtUtc + (duration ?? TimeSpan.FromHours(1)),
            timeZone = "Europe/Istanbul",
            recurrenceRule,
            reminderOffsetMinutes = 60,
            locationLabel = "Zoom",
            notes = (string?)null,
            allowTimeOffOverride,
        });

    private static Task<HttpResponseMessage> RescheduleAsync(HttpClient teacher, Guid lessonId, DateTime newStartAtUtc, bool allowTimeOffOverride)
        => teacher.PostAsJsonAsync($"/api/scheduling/lessons/{lessonId}/reschedule", new
        {
            newStartAtUtc,
            newEndAtUtc = newStartAtUtc.AddHours(1),
            note = "Ertelendi",
            allowTimeOffOverride,
        });

    private static Task<HttpResponseMessage> CreateTimeOffAsync(HttpClient teacher, Guid teacherUserId, DateTime startAtUtc, DateTime endAtUtc)
        => teacher.PostAsJsonAsync($"/api/scheduling/teachers/{teacherUserId}/time-off", new
        {
            type = 1, // TimeOffType.Holiday
            title = "Tatil",
            startAtUtc,
            endAtUtc,
            isAllDay = false,
        });

    private static async Task<int> CountLessonsAsync(WebApplicationFactory<Program> factory, Guid teacherUserId, DateTime startAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().LessonSchedules
            .CountAsync(lesson => lesson.TeacherUserId == teacherUserId && lesson.StartAtUtc == startAtUtc);
    }

    private static async Task<(HttpClient Teacher, Guid TeacherUserId, Guid StudentId)> CreateTeacherWithStudentAsync(
        WebApplicationFactory<Program> factory, string forwardedFor, string label)
    {
        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        var register = await teacher.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"cakisma-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = label,
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { 2 },
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        Guid teacherUserId;
        using (var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync()))
        {
            teacher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
            teacherUserId = payload.RootElement.GetProperty("userId").GetGuid();
        }

        var studentId = await ReadIdAsync(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName = $"{label} Ogrenci",
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin = 1,   // StudentOrigin.TeacherManaged
            subjects = Array.Empty<object>(),
        }));

        return (teacher, teacherUserId, studentId);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
