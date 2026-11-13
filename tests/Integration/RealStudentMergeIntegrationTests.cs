using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// Ö-C/B5: Profil birleştirme (merge) yayıldığında, modüller-arası handler'ların kaynak <c>StudentId</c>'ye
/// ait kayıtları gerçek Postgres üzerinde kanonik <c>StudentId</c>'ye yeniden atadığını doğrular. InMemory
/// sağlayıcısı <c>ExecuteUpdate/ExecuteDelete</c> desteklemediğinden bu davranış yalnız gerçek DB'de zorlanır.
/// </summary>
[Collection("containers")]
public sealed class RealStudentMergeIntegrationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Merge_Event_Reassigns_All_Source_Student_Records_To_Canonical()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacherUserId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var fromStudentId = Guid.NewGuid();
        var toStudentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var scheduleId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Kaynak (manuel) öğrenciye ait kayıtları modüllerin şemalarına serpiştir.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            scheduling.LessonSchedules.Add(new LessonSchedule(
                scheduleId, teacherUserId, fromStudentId, "Matematik", ScheduledLessonFormat.Online,
                now, now.AddHours(1), "Europe/Istanbul", null, LessonScheduleStatus.Planned, 30, null, null, null, now));
            await scheduling.SaveChangesAsync();

            var payments = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            payments.PaymentRecords.Add(new PaymentRecord(
                paymentId, teacherUserId, fromStudentId, null, BillingItemType.LessonFee, "Ders ücreti", "TRY",
                100m, 0m, now.AddDays(7), null, PaymentStatus.Pending, null, null, null, now));
            await payments.SaveChangesAsync();

            var sessions = scope.ServiceProvider.GetRequiredService<LessonSessionsDbContext>();
            sessions.LessonSessions.Add(new LessonSession(
                sessionId, null, teacherUserId, fromStudentId, "Matematik", now, null, null, null,
                StudentAttendanceStatus.Unknown, LessonSessionStatus.Planned, "Türev", null, null, now, null));
            await sessions.SaveChangesAsync();

            var study = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            study.StudyStudents.Add(new StudyStudent(fromStudentId, studentUserId, now));
            await study.SaveChangesAsync();
        }

        // Merge integration event'ini yayınla (Students domain event adıyla eşleşir).
        var mergeEvent = new IntegrationEvent(
            Guid.NewGuid(), now, "StudentProfilesMergedDomainEvent", "Students",
            JsonSerializer.Serialize(new StudentProfilesMergedIntegrationEvent(fromStudentId, toStudentId), IntegrationEventSerialization.Options));

        var eventBus = factory.Services.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(mergeEvent);

        // Doğrula: kaynak StudentId'ye ait hiçbir kayıt kalmadı; FK kayıtları kanonik'e taşındı, StudyStudent silindi.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            Assert.Equal(0, await scheduling.LessonSchedules.CountAsync(x => x.StudentId == fromStudentId));
            Assert.Equal(toStudentId, (await scheduling.LessonSchedules.AsNoTracking().SingleAsync(x => x.Id == scheduleId)).StudentId);

            var payments = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            Assert.Equal(0, await payments.PaymentRecords.CountAsync(x => x.StudentId == fromStudentId));
            Assert.Equal(toStudentId, (await payments.PaymentRecords.AsNoTracking().SingleAsync(x => x.Id == paymentId)).StudentId);

            var sessions = scope.ServiceProvider.GetRequiredService<LessonSessionsDbContext>();
            Assert.Equal(0, await sessions.LessonSessions.CountAsync(x => x.StudentId == fromStudentId));
            Assert.Equal(toStudentId, (await sessions.LessonSessions.AsNoTracking().SingleAsync(x => x.Id == sessionId)).StudentId);

            // StudyStudent birincil anahtarı öğrenci kimliği olduğundan kaynak satır silinir (kanonik korunur).
            var study = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            Assert.Equal(0, await study.StudyStudents.CountAsync(x => x.Id == fromStudentId));
        }
    }

    /// <summary>
    /// P15 Task 3 / N-28: Kendi profili olan öğrenci öğretmeninin davet kodunu kullanınca manuel profil kanonik
    /// profile birleşir. Birleşme yarım kalmamalı: öğretmen bağı (ücretiyle), bekleyen veli daveti, veli bağları,
    /// veli paneli özeti ve gelişim verisi kanonik kimliğe geçer. Fikstür <b>iki kimlikte de</b> satır içerir —
    /// benzersiz indeksler (<c>(TeacherUserId, StudentId)</c>, <c>known_students</c>, <c>child_progress_snapshots</c>,
    /// <c>topic_masteries</c>, <c>progress_snapshots</c>) yalnız gerçek Postgres'te çakışır.
    /// </summary>
    [SkippableFact]
    public async Task Claim_With_Existing_Profile_Merges_Links_Invites_And_Projections_Into_Canonical()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.153.0.1");
        var student = NewClient(factory, "10.153.0.2");
        var secondParent = NewClient(factory, "10.153.0.3");
        var teacherId = await RegisterAsync(teacher, role: 2, "Birlesme-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, "Birlesme-Ogrenci");
        await RegisterAsync(secondParent, role: 4, "Birlesme-Veli2");
        var firstParentUserId = Guid.NewGuid();

        // Öğrencinin kendi açtığı profil (kanonik) ve öğretmenin aynı öğrenci için açtığı manuel profil.
        var canonicalId = await CreateProfileAsync(student, userId: studentUserId, createdByTeacherUserId: null, origin: 2);
        var manualId = await CreateProfileAsync(teacher, userId: null, createdByTeacherUserId: teacherId, origin: 1);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PutAsJsonAsync(
            $"/api/students/teachers/{teacherId}/students/{manualId}/rate", new { agreedRateAmount = 750m, currency = "TRY" })).StatusCode);
        var parentInvite = await teacher.PostAsJsonAsync(
            $"/api/students/profiles/{manualId}/parent-invite", new { teacherUserId = teacherId, childDisplayName = "Ada" });
        Assert.Equal(HttpStatusCode.OK, parentInvite.StatusCode);
        var parentInviteCode = (await ReadJsonAsync(parentInvite)).GetProperty("inviteCode").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsJsonAsync(
            $"/api/students/teachers/{teacherId}/students/{manualId}/invite", new { targetUserId = studentUserId })).StatusCode);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var now = DateTime.UtcNow;
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                // Öğretmenin kanonik profilde ARŞİVLİ eski bağı: çakışmada arşivli olan düşer, kabul edilen davet taşınır.
                // Aynı SaveChanges'te silme + taşıma → (TeacherUserId, StudentId) benzersiz indeksi gerçek DB'de sınanır.
                var students = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
                var archived = new TeacherStudentLink(Guid.NewGuid(), teacherId, canonicalId, TeacherStudentLinkStatus.Linked, now);
                archived.Archive(now);
                students.TeacherStudentLinks.Add(archived);
                await students.SaveChangesAsync();

                // Aynı veli iki profile de bağlı (biri onaylı birincil, biri bekleyen) + iki profilde de veli paneli özeti.
                var parents = scope.ServiceProvider.GetRequiredService<ParentsDbContext>();
                var approvedOnManual = new ParentChildLink(Guid.NewGuid(), firstParentUserId, manualId, "Ada", "Anne", null, true, now);
                approvedOnManual.Approve(firstParentUserId, null, now);
                parents.ParentChildLinks.Add(approvedOnManual);
                parents.ParentChildLinks.Add(new ParentChildLink(Guid.NewGuid(), firstParentUserId, canonicalId, "Ada", "Anne", null, true, now));
                var manualSnapshot = new ChildProgressSnapshot(Guid.NewGuid(), manualId, now);
                manualSnapshot.RegisterPlannedLesson(now);
                manualSnapshot.RegisterPlannedLesson(now);
                manualSnapshot.RegisterCompletedLesson(now, now);
                var canonicalSnapshot = new ChildProgressSnapshot(Guid.NewGuid(), canonicalId, now);
                canonicalSnapshot.RegisterPlannedLesson(now);
                parents.ChildProgressSnapshots.AddRange(manualSnapshot, canonicalSnapshot);
                await parents.SaveChangesAsync();

                // Gelişim: (Matematik, Türev) iki profilde de var → çakışır; (Fizik, Kuvvet) yalnız manuelde.
                var progress = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
                var manualMastery = new TopicMastery(Guid.NewGuid(), manualId, "Matematik", "Türev", now);
                manualMastery.RegisterStudy(60, now);
                var canonicalMastery = new TopicMastery(Guid.NewGuid(), canonicalId, "Matematik", "Türev", now);
                canonicalMastery.RegisterStudy(30, now);
                progress.TopicMasteries.AddRange(manualMastery, canonicalMastery, new TopicMastery(Guid.NewGuid(), manualId, "Fizik", "Kuvvet", now));
                progress.TopicGoals.Add(new TopicGoal(
                    Guid.NewGuid(), manualId, "Matematik", "Türev", MasteryLevel.Proficient, null, teacherId, TopicGoalSetterRole.Teacher, null, now));
                progress.ProgressSnapshots.AddRange(
                    new ProgressSnapshot(Guid.NewGuid(), manualId, "Matematik", SnapshotPeriod.Weekly, weekStart, 40, 60, 0, 0m, now),
                    new ProgressSnapshot(Guid.NewGuid(), canonicalId, "Matematik", SnapshotPeriod.Weekly, weekStart, 20, 30, 0, 0m, now),
                    new ProgressSnapshot(Guid.NewGuid(), manualId, "Fizik", SnapshotPeriod.Weekly, weekStart, 10, 15, 0, 0m, now));
                await progress.SaveChangesAsync();
            }

            await DrainOutboxAsync(factory);

            // Öğrenci davet kodunu (küçük harfle) kullanır → kendi profili olduğu için birleşme.
            var code = await ReadInviteCodeAsync(factory, teacherId, manualId);
            var claim = await student.PostAsJsonAsync("/api/students/links/claim", new { inviteCode = code.ToLowerInvariant() });
            Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);

            await DrainOutboxAsync(factory);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                // Students: profil birleşti, bağ ücretiyle kanonikte, arşivli bağ düştü, bekleyen veli daveti taşındı.
                var students = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
                var manual = await students.StudentProfiles.AsNoTracking().SingleAsync(x => x.Id == manualId);
                Assert.True(manual.IsMerged);
                Assert.Equal(canonicalId, manual.MergedIntoStudentId);
                Assert.Equal(0, await students.TeacherStudentLinks.CountAsync(x => x.StudentId == manualId));
                var link = await students.TeacherStudentLinks.AsNoTracking().SingleAsync(x => x.TeacherUserId == teacherId && x.StudentId == canonicalId);
                Assert.Equal(TeacherStudentLinkStatus.Linked, link.Status);
                Assert.False(link.IsArchived);
                Assert.Equal(750m, link.AgreedRateAmount);
                Assert.Equal(canonicalId, (await students.StudentParentInvites.AsNoTracking().SingleAsync(x => x.InviteCode == parentInviteCode)).StudentId);
                // Manuel profildeki onaylı veli, boş olan kanonik profile taşınır (T4'e kadar veli okuması bu alana bakar).
                Assert.Equal(firstParentUserId, (await students.StudentProfiles.AsNoTracking().SingleAsync(x => x.Id == canonicalId)).ParentUserId);

                var access = scope.ServiceProvider.GetRequiredService<ITeacherStudentAccess>();
                Assert.Equal(TeacherAccessScope.Private, await access.GetScopeAsync(teacherId, canonicalId, CancellationToken.None));

                // Parents: aynı velinin tek yaşayan bağı kanonikte (onaylı + birincil), manuel profil hiçbir veliye görünmez.
                var parents = scope.ServiceProvider.GetRequiredService<ParentsDbContext>();
                Assert.Equal(0, await parents.ParentChildLinks.CountAsync(x => x.StudentId == manualId));
                var liveLinks = await parents.ParentChildLinks.AsNoTracking()
                    .Where(x => x.ParentUserId == firstParentUserId
                        && (x.Status == ParentChildLinkStatus.Approved || x.Status == ParentChildLinkStatus.Pending))
                    .ToListAsync();
                var live = Assert.Single(liveLinks);
                Assert.Equal(canonicalId, live.StudentId);
                Assert.Equal(ParentChildLinkStatus.Approved, live.Status);
                Assert.True(live.IsPrimaryContact);
                Assert.Equal(0, await parents.KnownStudents.CountAsync(x => x.StudentId == manualId));
                Assert.Equal(studentUserId, (await parents.KnownStudents.AsNoTracking().SingleAsync(x => x.StudentId == canonicalId)).UserId);
                Assert.Equal(0, await parents.ChildProgressSnapshots.CountAsync(x => x.StudentId == manualId));
                var panel = await parents.ChildProgressSnapshots.AsNoTracking().SingleAsync(x => x.StudentId == canonicalId);
                Assert.Equal(3, panel.PlannedLessonCount);
                Assert.Equal(1, panel.CompletedLessonCount);

                // ProgressTracking: çakışmayan satırlar taşındı, çakışanda kanonik kazandı, manuelde satır kalmadı.
                var progress = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
                Assert.Equal(0, await progress.TopicMasteries.CountAsync(x => x.StudentId == manualId));
                Assert.Equal(0, await progress.ProgressSnapshots.CountAsync(x => x.StudentId == manualId));
                Assert.Equal(0, await progress.TopicGoals.CountAsync(x => x.StudentId == manualId));
                Assert.Equal(30, (await progress.TopicMasteries.AsNoTracking().SingleAsync(
                    x => x.StudentId == canonicalId && x.Subject == "Matematik" && x.Topic == "Türev")).TotalStudyMinutes);
                Assert.True(await progress.TopicMasteries.AnyAsync(x => x.StudentId == canonicalId && x.Subject == "Fizik"));
                Assert.Equal(2, await progress.ProgressSnapshots.CountAsync(x => x.StudentId == canonicalId));
                Assert.Equal(1, await progress.TopicGoals.CountAsync(x => x.StudentId == canonicalId));
            }

            // HTTP: öğretmen kanonik profili listesinde görür, açar ve ona ders yazabilir.
            var list = await ReadJsonAsync(await teacher.GetAsync($"/api/students/profiles/by-teacher/{teacherId}"));
            var listedIds = list.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
            Assert.Contains(canonicalId, listedIds);
            Assert.DoesNotContain(manualId, listedIds);
            Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync($"/api/students/profiles/{canonicalId}")).StatusCode);
            var startAtUtc = DateTime.UtcNow.Date.AddDays(2).AddHours(15);
            var lesson = await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
            {
                teacherUserId = teacherId,
                studentId = canonicalId,
                subject = "Matematik",
                lessonFormat = 2,
                startAtUtc,
                endAtUtc = startAtUtc.AddHours(1),
                timeZone = "Europe/Istanbul",
                recurrenceRule = (string?)null,
                reminderOffsetMinutes = 60,
                locationLabel = "Zoom",
                notes = (string?)null
            });
            Assert.Equal(HttpStatusCode.OK, lesson.StatusCode);

            // Birleşmeden önce üretilmiş veli kodu artık kanonik profile bağlar.
            var parentClaim = await secondParent.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode = parentInviteCode });
            Assert.Equal(HttpStatusCode.OK, parentClaim.StatusCode);
            Assert.Equal(canonicalId, (await ReadJsonAsync(parentClaim)).GetProperty("studentId").GetGuid());
        }
        finally
        {
            // ProgressSnapshotProcessor paylaşılan Postgres'teki BÜTÜN hâkimiyet satırlarını tarar
            // (RealProgressSnapshotTests üretilen fotoğraf sayısını sayıyor) — bu testin satırları kalmamalı.
            await using var cleanup = factory.Services.CreateAsyncScope();
            var progress = cleanup.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
            await progress.TopicMasteries.Where(x => x.StudentId == manualId || x.StudentId == canonicalId).ExecuteDeleteAsync();
            await progress.ProgressSnapshots.Where(x => x.StudentId == manualId || x.StudentId == canonicalId).ExecuteDeleteAsync();
            await progress.TopicGoals.Where(x => x.StudentId == manualId || x.StudentId == canonicalId).ExecuteDeleteAsync();
        }
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> factory, string forwardedFor)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role, string label)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"birlesme-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = label,
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { role },
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
        return payload.RootElement.GetProperty("userId").GetGuid();
    }

    private static async Task<Guid> CreateProfileAsync(HttpClient client, Guid? userId, Guid? createdByTeacherUserId, int origin)
    {
        var response = await client.PostAsJsonAsync("/api/students/profiles", new
        {
            userId,
            createdByTeacherUserId,
            parentUserId = (Guid?)null,
            fullName = "Ada Sahin",
            gradeLevel = "7. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin,
            subjects = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Davet kodu bugün hiçbir API yanıtında yok (Öğrenilenler #184) — test onu salt-okunur DB'den alır.</summary>
    private static async Task<string> ReadInviteCodeAsync(WebApplicationFactory<Program> factory, Guid teacherId, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var students = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
        var link = await students.TeacherStudentLinks.AsNoTracking()
            .SingleAsync(x => x.TeacherUserId == teacherId && x.StudentId == studentId);
        return link.InviteCode!;
    }

    /// <summary>Aynı Postgres'i paylaşan testlerin satırları partiyi doldurabilir; kuyruk boşalana kadar tur çevrilir.</summary>
    private static async Task DrainOutboxAsync(WebApplicationFactory<Program> factory)
    {
        for (var round = 0; round < 30; round++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync() == 0)
            {
                return;
            }
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
