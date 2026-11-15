using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Payments.Domain;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Modules.Reporting.Domain;
using EgitimUssu.Modules.Reporting.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealTeacherFinanceScenarioTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Completed_lesson_cash_parent_confirmation_stale_details_and_refund_preserve_finances()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var infrastructure = RealInfrastructure.Use(fixture);
        var barrier = new StaleDetailsBarrier();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>(); // Outbox/tarayıcı adımlarını senaryo sürer.
                services.AddDbContext<PaymentsDbContext>(options => options.AddInterceptors(barrier));
            }));
        using var teacher = Client(factory, "10.196.1.1");
        using var secondDevice = Client(factory, "10.196.1.2");
        using var parent = Client(factory, "10.196.1.3");
        var teacherId = await Register(teacher, 2);
        secondDevice.DefaultRequestHeaders.Authorization = teacher.DefaultRequestHeaders.Authorization;
        var parentId = await Register(parent, 4);
        var studentId = (await Json(await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            createdByTeacherUserId = teacherId, fullName = "Finans Öğrencisi", gradeLevel = "8", origin = 1,
            subjects = Array.Empty<object>()
        }))).GetProperty("id").GetGuid();
        try
        {
            using (var rate = await teacher.PutAsJsonAsync($"/api/students/teachers/{teacherId}/students/{studentId}/rate",
                new { agreedRateAmount = 500m, currency = "TRY" }))
                Assert.Equal(HttpStatusCode.NoContent, rate.StatusCode);
            var invite = await Json(await teacher.PostAsJsonAsync($"/api/students/profiles/{studentId}/parent-invite",
                new { teacherUserId = teacherId, childDisplayName = "Finans Öğrencisi" }));
            var link = await Json(await parent.PostAsJsonAsync("/api/parents/children/claim-invite",
                new { inviteCode = invite.GetProperty("inviteCode").GetString() }));
            Assert.Equal("Approved", link.GetProperty("status").GetString());

            var now = DateTime.UtcNow;
            var start = now.AddHours(-2);
            var lesson = await Json(await teacher.PostAsJsonAsync("/api/scheduling/lessons", new
            {
                teacherUserId = teacherId, studentId, subject = "Matematik", lessonFormat = 2,
                startAtUtc = start, endAtUtc = start.AddHours(1), timeZone = "Europe/Istanbul", reminderOffsetMinutes = 15
            }));
            var scheduleId = lesson.GetProperty("id").GetGuid();
            var session = await Json(await teacher.PostAsJsonAsync("/api/lesson-sessions/from-schedule",
                new { lessonScheduleId = scheduleId, occurrenceStartAtUtc = start }));
            var sessionId = session.GetProperty("id").GetGuid();
            await Json(await teacher.PostAsJsonAsync($"/api/lesson-sessions/{sessionId}/complete", new
            {
                actualStartAtUtc = start, actualEndAtUtc = start.AddHours(1), attendanceStatus = 2,
                topicTitle = "Kesirler", teacherNotes = "Finans kapanışı", isChargeable = true
            }));
            await Drain(factory);

            // Otomatik ücret gerçek tamamlama olayından gelmeli; elle ödeme oluşturma yok.
            var records = await Json(await teacher.GetAsync($"/api/payments/teachers/{teacherId}/records?outstandingOnly=false"));
            var payment = Assert.Single(records.EnumerateArray());
            var paymentId = payment.GetProperty("id").GetGuid();
            Assert.Equal(sessionId, payment.GetProperty("relatedLessonSessionId").GetGuid());
            Assert.Equal(500m, payment.GetProperty("expectedAmount").GetDecimal());
            Assert.Equal("TRY", payment.GetProperty("currency").GetString());
            AssertMoney(payment, 0m, "Pending");

            payment = await Json(await teacher.PostAsJsonAsync($"/api/payments/records/{paymentId}/collections",
                new { amount = 200m, method = 1, occurredOnUtc = now, note = "Nakit" }));
            AssertMoney(payment, 200m, "PartiallyPaid");
            // İkinci cihaz onaydan önceki 200 tahsil/PartiallyPaid ekranını tutar.
            var staleDetails = new
            {
                description = payment.GetProperty("description").GetString(), expectedAmount = 500m,
                dueDateUtc = payment.GetProperty("dueDateUtc").GetDateTime(), notes = StaleDetailsBarrier.Note,
                collectedAmount = 200m, status = 2, currency = "USD"
            };
            var declaration = await Json(await parent.PostAsJsonAsync($"/api/payments/records/{paymentId}/declare-paid",
                new { declaredAmount = 300m, note = "Kalan tutarı havale ettim" }));
            var declarationId = declaration.GetProperty("id").GetGuid();
            AssertMoney(await Json(await teacher.GetAsync($"/api/payments/records/{paymentId}")), 200m, "PartiallyPaid");

            barrier.PaymentId = paymentId;
            var staleWrite = secondDevice.PutAsJsonAsync($"/api/payments/records/{paymentId}", staleDetails);
            JsonElement confirmation;
            try
            {
                await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
                confirmation = await Json(await teacher.PostAsync($"/api/payments/payment-declarations/{declarationId}/confirm", null));
            }
            finally { barrier.Release.TrySetResult(); }
            using (var conflict = await staleWrite)
            {
                Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
                using var body = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
                Assert.Equal("shared.concurrency_conflict", body.RootElement.GetProperty("code").GetString());
            }
            Assert.Equal("Confirmed", confirmation.GetProperty("status").GetString());
            barrier.PaymentId = Guid.Empty;
            payment = await Json(await secondDevice.PutAsJsonAsync($"/api/payments/records/{paymentId}", staleDetails));
            AssertMoney(payment, 500m, "Paid");
            Assert.Equal("TRY", payment.GetProperty("currency").GetString());
            Assert.Equal(StaleDetailsBarrier.Note, payment.GetProperty("notes").GetString());
            Assert.Equal(2, payment.GetProperty("transactions").GetArrayLength());

            payment = await Json(await teacher.PostAsJsonAsync($"/api/payments/records/{paymentId}/refunds",
                new { amount = 100m, method = 1, occurredOnUtc = now, note = "Kısmi iade" }));
            AssertMoney(payment, 400m, "PartiallyPaid");
            Assert.Equal(100m, payment.GetProperty("outstandingAmount").GetDecimal());
            await Drain(factory);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var stored = await db.PaymentRecords.AsNoTracking().SingleAsync(p => p.Id == paymentId);
            Assert.Equal(3, stored.Transactions.Count);
            Assert.Equal(200m, Assert.Single(stored.Transactions, t => t.Kind == PaymentTransactionKind.Collection && t.Method == PaymentMethod.Cash).Amount);
            Assert.Equal(300m, Assert.Single(stored.Transactions, t => t.Method == PaymentMethod.ParentDeclaration).Amount);
            Assert.Equal(100m, Assert.Single(stored.Transactions, t => t.Kind == PaymentTransactionKind.Refund).Amount);
            var storedDeclaration = await db.ParentPaymentDeclarations.AsNoTracking().SingleAsync(d => d.Id == declarationId);
            Assert.Equal(parentId, storedDeclaration.ParentUserId);
            Assert.Equal(ParentPaymentDeclarationStatus.Confirmed, storedDeclaration.Status);
            Assert.Equal(3, await db.OutboxMessages.CountAsync(m => m.Type == "PaymentTransactionRecordedIntegrationEvent" && m.Payload.Contains(paymentId.ToString())));

            var date = scope.ServiceProvider.GetRequiredService<ILocalCalendar>().ToLocalDate(now);
            var monthly = await Json(await teacher.GetAsync($"/api/reporting/teachers/{teacherId}/monthly?year={date.Year}&month={date.Month}"));
            Assert.Equal(1, monthly.GetProperty("completedLessonCount").GetInt32());
            Assert.Equal(500m, monthly.GetProperty("expectedAmount").GetDecimal());
            Assert.Equal(400m, monthly.GetProperty("collectedAmount").GetDecimal());
            var projection = await scope.ServiceProvider.GetRequiredService<ReportingDbContext>().TeacherMonthlyProjections
                .AsNoTracking().Include(p => p.Items).SingleAsync(p => p.TeacherUserId == teacherId && p.Year == date.Year && p.Month == date.Month);
            Assert.Equal(100m, projection.RefundedAmount);
            Assert.Equal(stored.Transactions.Select(t => t.Id).Order(), projection.Items
                .Where(i => i.Kind is TeacherMonthlyItemKind.Collection or TeacherMonthlyItemKind.Refund).Select(i => i.ItemId).Order());
        }
        finally
        {
            // Ortak DB kullanan snapshot testine bu öğrencinin konu kaydını bırakma.
            await using var cleanup = factory.Services.CreateAsyncScope();
            await cleanup.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>().TopicMasteries
                .Where(t => t.StudentId == studentId).ExecuteDeleteAsync();
        }
    }

    private static async Task Drain(WebApplicationFactory<Program> factory)
    {
        for (var i = 0; i < 30; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync() == 0) return;
        }
        Assert.Fail("Outbox boşalmadı.");
    }

    private static void AssertMoney(JsonElement record, decimal collected, string status)
    {
        Assert.Equal(collected, record.GetProperty("collectedAmount").GetDecimal());
        Assert.Equal(status, record.GetProperty("status").GetString());
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory, string ip)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    private static async Task<Guid> Register(HttpClient client, int role)
    {
        var auth = await Json(await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"finance-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Finans", lastName = "Test", roles = new[] { role }
        }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return auth.GetProperty("userId").GetGuid();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
            using var json = JsonDocument.Parse(body);
            return json.RootElement.Clone();
        }
    }

    private sealed class StaleDetailsBarrier : SaveChangesInterceptor
    {
        public const string Note = "İkinci cihazdan güncel not";
        public Guid PaymentId { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (PaymentId != Guid.Empty && eventData.Context!.ChangeTracker.Entries<PaymentRecord>()
                .Any(e => e.Entity.Id == PaymentId && e.Entity.Notes == Note && e.State == EntityState.Modified))
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
