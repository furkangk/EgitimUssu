using System.Text.Json;
using EgitimUssu.Modules.LessonSessions.Domain;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
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
}
