using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Modules.LessonSessions.Infrastructure;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace EgitimUssu.Tests.Integration;

public sealed class TeacherWorkflowIntegrationTests
{
    [Fact]
    public async Task Teacher_Workflow_Should_Create_Profile_Student_Lesson_Session_FollowUp_And_Payment()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        var previousOutboxDispatchEnabled = Environment.GetEnvironmentVariable("Outbox__DispatchEnabled");
        var previousOutboxPollInterval = Environment.GetEnvironmentVariable("Outbox__PollIntervalSeconds");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "InMemory:workflow-tests");
        Environment.SetEnvironmentVariable("Outbox__DispatchEnabled", "true");
        Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", "1");

        try
        {
            await using var factory = new WebApplicationFactory<Program>();

            using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = "teacher1@example.com",
            password = "Teacher123!",
            firstName = "Ayse",
            lastName = "Yilmaz",
            phoneNumber = "5551112233",
            roles = new[] { 2 }
        });

        registerResponse.EnsureSuccessStatusCode();
        var authPayload = await ReadJsonAsync(registerResponse);
        var teacherUserId = authPayload.GetProperty("userId").GetGuid();
        var accessToken = authPayload.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var teacherProfileResponse = await client.PostAsJsonAsync("/api/teachers/profiles", new
        {
            userId = teacherUserId,
            fullName = "Ayse Yilmaz",
            subject = "Matematik",
            city = "Istanbul",
            district = "Kadikoy",
            biography = "Deneyimli matematik ogretmeni",
            headline = "LGS ve TYT",
            lessonFormat = 3,
            experienceYears = 8,
            educationLevel = "Lisans",
            hourlyRateAmount = 1250,
            currency = "TRY",
            isVerified = false,
            profilePhotoUrl = "https://example.com/teacher.png",
            availabilitySlots = new[]
            {
                new
                {
                    dayOfWeek = 1,
                    startTime = "18:00:00",
                    endTime = "20:00:00",
                    isOnlineAvailable = true,
                    isInPersonAvailable = true
                }
            }
        });

        teacherProfileResponse.EnsureSuccessStatusCode();

        var studentResponse = await client.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName = "Mehmet Demir",
            gradeLevel = "8. Sinif",
            contactEmail = "veli@example.com",
            contactPhone = "5554443322",
            goalSummary = "LGS hazirligi",
            levelNotes = "Cebir guclendirilecek",
            origin = 1,
            subjects = new[]
            {
                new
                {
                    subject = "Matematik",
                    targetLevel = "LGS 2026"
                }
            }
        });

        studentResponse.EnsureSuccessStatusCode();
        var studentPayload = await ReadJsonAsync(studentResponse);
        var studentId = studentPayload.GetProperty("id").GetGuid();

        var studentsByTeacherResponse = await client.GetAsync($"/api/students/profiles/by-teacher/{teacherUserId}");
        studentsByTeacherResponse.EnsureSuccessStatusCode();
        var studentsByTeacherPayload = await ReadJsonAsync(studentsByTeacherResponse);
        Assert.Single(studentsByTeacherPayload.EnumerateArray());

        var startAtUtc = new DateTime(2026, 05, 12, 15, 0, 0, DateTimeKind.Utc);
        var endAtUtc = startAtUtc.AddHours(1);

        var lessonResponse = await client.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId = teacherUserId,
            studentId,
            subject = "Matematik",
            lessonFormat = 2,
            startAtUtc,
            endAtUtc,
            timeZone = "Europe/Istanbul",
            recurrenceRule = "FREQ=WEEKLY;COUNT=4",
            reminderOffsetMinutes = 60,
            locationLabel = "Zoom",
            notes = "Haftalik tekrar"
        });

        lessonResponse.EnsureSuccessStatusCode();
        var lessonPayload = await ReadJsonAsync(lessonResponse);
        var lessonId = lessonPayload.GetProperty("id").GetGuid();

        var reminderReady = await WaitForReminderAsync(factory, lessonId);
        Assert.True(reminderReady);

        var reminderResponse = await client.GetAsync($"/api/notifications/teachers/{teacherUserId}/lesson-reminders?activeOnly=false");
        reminderResponse.EnsureSuccessStatusCode();
        var reminderPayload = await ReadJsonAsync(reminderResponse);
        Assert.Contains(reminderPayload.EnumerateArray(), item => item.GetProperty("lessonScheduleId").GetGuid() == lessonId);

        var teacherLessonsResponse = await client.GetAsync($"/api/scheduling/teachers/{teacherUserId}/lessons?startAtUtc={Uri.EscapeDataString(startAtUtc.AddDays(-1).ToString("O"))}&endAtUtc={Uri.EscapeDataString(endAtUtc.AddDays(7).ToString("O"))}");
        teacherLessonsResponse.EnsureSuccessStatusCode();
        var teacherLessonsPayload = await ReadJsonAsync(teacherLessonsResponse);
        Assert.Single(teacherLessonsPayload.EnumerateArray());

        var conflictResponse = await client.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId = teacherUserId,
            studentId,
            subject = "Geometri",
            lessonFormat = 2,
            startAtUtc = startAtUtc.AddMinutes(30),
            endAtUtc = endAtUtc.AddMinutes(30),
            timeZone = "Europe/Istanbul",
            recurrenceRule = (string?)null,
            reminderOffsetMinutes = 30,
            locationLabel = "Zoom",
            notes = "Cakismali deneme"
        });

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        var conflictPayload = await ReadJsonAsync(conflictResponse);
        Assert.Equal("scheduling.teacher_conflict", conflictPayload.GetProperty("code").GetString());
        Assert.True(conflictPayload.TryGetProperty("message", out _));
        Assert.True(conflictPayload.TryGetProperty("errors", out _));
        Assert.Equal(409, conflictPayload.GetProperty("status").GetInt32());
        Assert.True(conflictPayload.TryGetProperty("traceId", out _));

        var sessionResponse = await client.PostAsJsonAsync("/api/lesson-sessions", new
        {
            lessonScheduleId = lessonId,
            teacherUserId = teacherUserId,
            studentId,
            subject = "Matematik",
            plannedStartAtUtc = startAtUtc,
            topicTitle = "Problemler"
        });

        sessionResponse.EnsureSuccessStatusCode();
        var sessionPayload = await ReadJsonAsync(sessionResponse);
        var sessionId = sessionPayload.GetProperty("id").GetGuid();

        var completeResponse = await client.PostAsJsonAsync($"/api/lesson-sessions/{sessionId}/complete", new
        {
            actualStartAtUtc = startAtUtc,
            actualEndAtUtc = endAtUtc,
            attendanceStatus = 2,
            topicTitle = "Problemler",
            coveredContent = "Oran oranti ve problem cozumu",
            teacherNotes = "Odev verilecek"
        });

        completeResponse.EnsureSuccessStatusCode();
        var completedPayload = await ReadJsonAsync(completeResponse);
        Assert.Equal("Completed", completedPayload.GetProperty("status").GetString());

        var autoFollowUpReady = false;
        for (var attempt = 0; attempt < 5 && !autoFollowUpReady; attempt++)
        {
            await using var dispatchScope = factory.Services.CreateAsyncScope();
            var outboxProcessor = dispatchScope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            await outboxProcessor.DispatchPendingAsync();

            var dispatchAssignmentsDb = dispatchScope.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
            autoFollowUpReady = await dispatchAssignmentsDb.LessonNotes
                .AnyAsync(note => note.LessonSessionId == sessionId);
        }

        var autoFollowUpResponse = await client.GetAsync($"/api/assignments/lesson-sessions/{sessionId}/follow-up");
        autoFollowUpResponse.EnsureSuccessStatusCode();
        var autoFollowUpPayload = await ReadJsonAsync(autoFollowUpResponse);
        Assert.Equal(sessionId, autoFollowUpPayload.GetProperty("lessonSessionId").GetGuid());
        Assert.Equal(0, autoFollowUpPayload.GetProperty("assignments").GetArrayLength());

        var followUpResponse = await client.PostAsJsonAsync($"/api/assignments/lesson-sessions/{sessionId}/follow-up", new
        {
            summary = "Problemler konusunda temel yaklaşım oturdu.",
            coveredTopics = "Oran oranti, yeni nesil problem kurma",
            recommendations = "Her gun 20 soru problem tekrar etsin.",
            assignments = new[]
            {
                new
                {
                    title = "Problem föyu 1",
                    description = "Sorularin tamami cozulup isaretlenecek",
                    dueDateUtc = endAtUtc.AddDays(2),
                    attachmentUrl = (string?)"https://example.com/problem-foyu-1.pdf"
                },
                new
                {
                    title = "Mini tekrar testi",
                    description = "20 soruluk sureli deneme",
                    dueDateUtc = endAtUtc.AddDays(4),
                    attachmentUrl = (string?)null
                }
            }
        });

        followUpResponse.EnsureSuccessStatusCode();
        var followUpPayload = await ReadJsonAsync(followUpResponse);
        Assert.Equal(sessionId, followUpPayload.GetProperty("lessonSessionId").GetGuid());
        Assert.Equal(2, followUpPayload.GetProperty("assignments").GetArrayLength());
        Assert.Equal("Problemler konusunda temel yaklaşım oturdu.", followUpPayload.GetProperty("note").GetProperty("summary").GetString());

        var getFollowUpResponse = await client.GetAsync($"/api/assignments/lesson-sessions/{sessionId}/follow-up");
        getFollowUpResponse.EnsureSuccessStatusCode();
        var getFollowUpPayload = await ReadJsonAsync(getFollowUpResponse);
        Assert.Equal("Problemler konusunda temel yaklaşım oturdu.", getFollowUpPayload.GetProperty("note").GetProperty("summary").GetString());

        var idempotentFollowUpResponse = await client.PostAsJsonAsync($"/api/assignments/lesson-sessions/{sessionId}/follow-up", new
        {
            summary = "Bu istek yeni kayit olusturmamali.",
            coveredTopics = "Farkli bir icerik",
            recommendations = "Farkli bir onerme",
            assignments = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Conflict, idempotentFollowUpResponse.StatusCode);

        var paymentCreateResponse = await client.PostAsJsonAsync("/api/payments/records", new
        {
            teacherUserId,
            studentId,
            relatedLessonSessionId = sessionId,
            itemType = 1,
            description = "12 Mayis Matematik dersi ucreti",
            currency = "TRY",
            expectedAmount = 1250,
            collectedAmount = 500,
            dueDateUtc = DateTime.UtcNow.AddDays(3),
            collectedOnUtc = endAtUtc,
            status = 2,
            billingPeriodStartUtc = startAtUtc.Date,
            billingPeriodEndUtc = startAtUtc.Date.AddDays(30),
            notes = "Ilk taksit alindi"
        });

        paymentCreateResponse.EnsureSuccessStatusCode();
        var paymentPayload = await ReadJsonAsync(paymentCreateResponse);
        var paymentRecordId = paymentPayload.GetProperty("id").GetGuid();
        Assert.Equal("PartiallyPaid", paymentPayload.GetProperty("status").GetString());
        Assert.Equal(750, paymentPayload.GetProperty("outstandingAmount").GetDecimal());

        var paymentUpdateResponse = await client.PutAsJsonAsync($"/api/payments/records/{paymentRecordId}", new
        {
            teacherUserId,
            studentId,
            relatedLessonSessionId = sessionId,
            itemType = 1,
            description = "12 Mayis Matematik dersi ucreti",
            currency = "TRY",
            expectedAmount = 1250,
            collectedAmount = 1250,
            dueDateUtc = DateTime.UtcNow.AddDays(3),
            collectedOnUtc = endAtUtc.AddDays(1),
            status = 3,
            billingPeriodStartUtc = startAtUtc.Date,
            billingPeriodEndUtc = startAtUtc.Date.AddDays(30),
            notes = "Tam tahsil edildi"
        });

        paymentUpdateResponse.EnsureSuccessStatusCode();
        var updatedPaymentPayload = await ReadJsonAsync(paymentUpdateResponse);
        Assert.Equal("Paid", updatedPaymentPayload.GetProperty("status").GetString());
        Assert.Equal(0, updatedPaymentPayload.GetProperty("outstandingAmount").GetDecimal());

        var paymentRecordResponse = await client.GetAsync($"/api/payments/records/{paymentRecordId}");
        paymentRecordResponse.EnsureSuccessStatusCode();
        var paymentRecordPayload = await ReadJsonAsync(paymentRecordResponse);
        Assert.Equal(paymentRecordId, paymentRecordPayload.GetProperty("id").GetGuid());

        var paymentListResponse = await client.GetAsync($"/api/payments/teachers/{teacherUserId}/records?outstandingOnly=false");
        paymentListResponse.EnsureSuccessStatusCode();
        var paymentListPayload = await ReadJsonAsync(paymentListResponse);
        Assert.Single(paymentListPayload.EnumerateArray());

        var outstandingPaymentListResponse = await client.GetAsync($"/api/payments/teachers/{teacherUserId}/records?outstandingOnly=true");
        outstandingPaymentListResponse.EnsureSuccessStatusCode();
        var outstandingPaymentListPayload = await ReadJsonAsync(outstandingPaymentListResponse);
        Assert.Empty(outstandingPaymentListPayload.EnumerateArray());

        var paymentSummaryResponse = await client.GetAsync($"/api/payments/teachers/{teacherUserId}/summary");
        paymentSummaryResponse.EnsureSuccessStatusCode();
        var paymentSummaryPayload = await ReadJsonAsync(paymentSummaryResponse);
        Assert.Equal(1, paymentSummaryPayload.GetProperty("totalRecords").GetInt32());
        Assert.Single(paymentSummaryPayload.GetProperty("currencySummaries").EnumerateArray());
        var currencySummary = paymentSummaryPayload.GetProperty("currencySummaries")[0];
        Assert.Equal("TRY", currencySummary.GetProperty("currency").GetString());
        Assert.Equal(1, currencySummary.GetProperty("paidCount").GetInt32());
        Assert.Equal(0, currencySummary.GetProperty("outstandingAmountTotal").GetDecimal());

            await using var scope = factory.Services.CreateAsyncScope();
            var assignmentsDb = scope.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
            var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var paymentsDb = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var schedulingDb = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var lessonSessionsDb = scope.ServiceProvider.GetRequiredService<LessonSessionsDbContext>();

            Assert.True(await schedulingDb.OutboxMessages.AnyAsync(message => message.Type == "LessonScheduledDomainEvent"));
            Assert.True(await lessonSessionsDb.OutboxMessages.AnyAsync(message => message.Type == "LessonSessionCompletedDomainEvent"));
            Assert.True(await assignmentsDb.OutboxMessages.AnyAsync(message => message.Type == "LessonNoteCreatedDomainEvent"));
            Assert.True(await assignmentsDb.OutboxMessages.AnyAsync(message => message.Type == "AssignmentCreatedDomainEvent"));
            Assert.True(await notificationsDb.OutboxMessages.AnyAsync(message => message.Type == "LessonReminderCreatedDomainEvent"));
            Assert.True(await paymentsDb.OutboxMessages.AnyAsync(message => message.Type == "PaymentRecordCreatedDomainEvent"));
            Assert.True(await paymentsDb.OutboxMessages.AnyAsync(message => message.Type == "PaymentRecordUpdatedDomainEvent"));
            Assert.Equal(1, await assignmentsDb.LessonNotes.CountAsync());
            Assert.Equal(2, await assignmentsDb.Assignments.CountAsync());
            Assert.Equal(1, await notificationsDb.LessonReminders.CountAsync());
            Assert.Equal(1, await paymentsDb.PaymentRecords.CountAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", previousConnectionString);
            Environment.SetEnvironmentVariable("Outbox__DispatchEnabled", previousOutboxDispatchEnabled);
            Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", previousOutboxPollInterval);
        }
    }

    [Fact]
    public async Task Protected_Endpoints_Should_Require_Authentication()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "InMemory:auth-tests");

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/teachers/profiles", new
        {
            userId = Guid.NewGuid(),
            fullName = "Unauthorized User",
            subject = "Matematik",
            city = "Istanbul",
            district = "Kadikoy",
            biography = "x",
            headline = "x",
            lessonFormat = 2,
            experienceYears = 5,
            educationLevel = "Lisans",
            hourlyRateAmount = 1000,
            currency = "TRY",
            isVerified = false,
            profilePhotoUrl = (string?)null,
            availabilitySlots = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var unauthorizedPayload = await ReadJsonAsync(response);
        Assert.Equal("shared.unauthorized", unauthorizedPayload.GetProperty("code").GetString());
        Assert.Equal(401, unauthorizedPayload.GetProperty("status").GetInt32());
        Assert.True(unauthorizedPayload.TryGetProperty("message", out _));
        Assert.True(unauthorizedPayload.TryGetProperty("errors", out _));
        Assert.True(unauthorizedPayload.TryGetProperty("traceId", out _));

            var paymentResponse = await client.PostAsJsonAsync("/api/payments/records", new
            {
                teacherUserId = Guid.NewGuid(),
                studentId = Guid.NewGuid(),
                relatedLessonSessionId = (Guid?)null,
                itemType = 1,
                description = "Unauthorized payment",
                currency = "TRY",
                expectedAmount = 1000,
                collectedAmount = 0,
                dueDateUtc = DateTime.UtcNow.AddDays(1),
                collectedOnUtc = (DateTime?)null,
                status = 1,
                billingPeriodStartUtc = (DateTime?)null,
                billingPeriodEndUtc = (DateTime?)null,
                notes = (string?)null
            });

            Assert.Equal(HttpStatusCode.Unauthorized, paymentResponse.StatusCode);
            var paymentUnauthorizedPayload = await ReadJsonAsync(paymentResponse);
            Assert.Equal("shared.unauthorized", paymentUnauthorizedPayload.GetProperty("code").GetString());
            Assert.Equal(401, paymentUnauthorizedPayload.GetProperty("status").GetInt32());

            var reminderResponse = await client.GetAsync($"/api/notifications/teachers/{Guid.NewGuid()}/lesson-reminders?activeOnly=true");
            Assert.Equal(HttpStatusCode.Unauthorized, reminderResponse.StatusCode);
            var reminderUnauthorizedPayload = await ReadJsonAsync(reminderResponse);
            Assert.Equal("shared.unauthorized", reminderUnauthorizedPayload.GetProperty("code").GetString());
            Assert.Equal(401, reminderUnauthorizedPayload.GetProperty("status").GetInt32());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", previousConnectionString);
        }
    }

    [Fact]
    public async Task Cancelled_Lesson_Should_Cancel_Related_Reminder()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        var previousOutboxDispatchEnabled = Environment.GetEnvironmentVariable("Outbox__DispatchEnabled");
        var previousOutboxPollInterval = Environment.GetEnvironmentVariable("Outbox__PollIntervalSeconds");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "InMemory:notification-cancel-tests");
        Environment.SetEnvironmentVariable("Outbox__DispatchEnabled", "true");
        Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", "1");

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            var registerResponse = await client.PostAsJsonAsync("/api/identity/register", new
            {
                email = "teacher2@example.com",
                password = "Teacher123!",
                firstName = "Fatma",
                lastName = "Kaya",
                phoneNumber = "5551112244",
                roles = new[] { 2 }
            });

            registerResponse.EnsureSuccessStatusCode();
            var authPayload = await ReadJsonAsync(registerResponse);
            var teacherUserId = authPayload.GetProperty("userId").GetGuid();
            var accessToken = authPayload.GetProperty("accessToken").GetString();
            Assert.False(string.IsNullOrWhiteSpace(accessToken));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var teacherProfileResponse = await client.PostAsJsonAsync("/api/teachers/profiles", new
            {
                userId = teacherUserId,
                fullName = "Fatma Kaya",
                subject = "Fizik",
                city = "Istanbul",
                district = "Besiktas",
                biography = "Fizik ogretmeni",
                headline = "AYT Fizik",
                lessonFormat = 2,
                experienceYears = 6,
                educationLevel = "Lisans",
                hourlyRateAmount = 1400,
                currency = "TRY",
                isVerified = false,
                profilePhotoUrl = (string?)null,
                availabilitySlots = Array.Empty<object>()
            });

            teacherProfileResponse.EnsureSuccessStatusCode();

            var studentResponse = await client.PostAsJsonAsync("/api/students/profiles", new
            {
                userId = (Guid?)null,
                createdByTeacherUserId = teacherUserId,
                parentUserId = (Guid?)null,
                fullName = "Ece Ak",
                gradeLevel = "11. Sinif",
                contactEmail = "veli2@example.com",
                contactPhone = "5554443323",
                goalSummary = "AYT Fizik",
                levelNotes = "Optik calisilacak",
                origin = 1,
                subjects = Array.Empty<object>()
            });

            studentResponse.EnsureSuccessStatusCode();
            var studentId = (await ReadJsonAsync(studentResponse)).GetProperty("id").GetGuid();

            var startAtUtc = new DateTime(2026, 06, 01, 14, 0, 0, DateTimeKind.Utc);
            var endAtUtc = startAtUtc.AddHours(1);
            var lessonResponse = await client.PostAsJsonAsync("/api/scheduling/lessons", new
            {
                teacherUserId,
                studentId,
                subject = "Fizik",
                lessonFormat = 2,
                startAtUtc,
                endAtUtc,
                timeZone = "Europe/Istanbul",
                recurrenceRule = (string?)null,
                reminderOffsetMinutes = 60,
                locationLabel = "Zoom",
                notes = "Deneme"
            });

            lessonResponse.EnsureSuccessStatusCode();
            var lessonId = (await ReadJsonAsync(lessonResponse)).GetProperty("id").GetGuid();

            var reminderReady = await WaitForReminderAsync(factory, lessonId);
            Assert.True(reminderReady);

            var cancelResponse = await client.PostAsJsonAsync($"/api/scheduling/lessons/{lessonId}/cancel", new
            {
                cancellationNote = "Ogrenci hastalandi"
            });

            cancelResponse.EnsureSuccessStatusCode();

            for (var attempt = 0; attempt < 10; attempt++)
            {
                await using var dispatchScope = factory.Services.CreateAsyncScope();
                var outboxProcessor = dispatchScope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                await outboxProcessor.DispatchPendingAsync();

                var notificationsDb = dispatchScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
                var reminder = await notificationsDb.LessonReminders.FirstOrDefaultAsync(item => item.LessonScheduleId == lessonId);
                if (reminder is not null && reminder.Status.ToString() == "Cancelled")
                {
                    break;
                }
            }

            var remindersResponse = await client.GetAsync($"/api/notifications/teachers/{teacherUserId}/lesson-reminders?activeOnly=false");
            remindersResponse.EnsureSuccessStatusCode();
            var remindersPayload = await ReadJsonAsync(remindersResponse);
            Assert.Single(remindersPayload.EnumerateArray());
            Assert.Equal("Cancelled", remindersPayload[0].GetProperty("status").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", previousConnectionString);
            Environment.SetEnvironmentVariable("Outbox__DispatchEnabled", previousOutboxDispatchEnabled);
            Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", previousOutboxPollInterval);
        }
    }

    [Fact]
    public async Task Due_Reminder_Should_Be_Dispatched_As_Sent()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        var previousOutboxDispatchEnabled = Environment.GetEnvironmentVariable("Outbox__DispatchEnabled");
        var previousOutboxPollInterval = Environment.GetEnvironmentVariable("Outbox__PollIntervalSeconds");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "InMemory:notification-send-tests");
        Environment.SetEnvironmentVariable("Outbox__DispatchEnabled", "true");
        Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", "1");

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            var registerResponse = await client.PostAsJsonAsync("/api/identity/register", new
            {
                email = "teacher3@example.com",
                password = "Teacher123!",
                firstName = "Zeynep",
                lastName = "Aras",
                phoneNumber = "5551112255",
                roles = new[] { 2 }
            });

            registerResponse.EnsureSuccessStatusCode();
            var authPayload = await ReadJsonAsync(registerResponse);
            var teacherUserId = authPayload.GetProperty("userId").GetGuid();
            var accessToken = authPayload.GetProperty("accessToken").GetString();
            Assert.False(string.IsNullOrWhiteSpace(accessToken));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var teacherProfileResponse = await client.PostAsJsonAsync("/api/teachers/profiles", new
            {
                userId = teacherUserId,
                fullName = "Zeynep Aras",
                subject = "Kimya",
                city = "Istanbul",
                district = "Sisli",
                biography = "Kimya ogretmeni",
                headline = "TYT Kimya",
                lessonFormat = 2,
                experienceYears = 7,
                educationLevel = "Lisans",
                hourlyRateAmount = 1350,
                currency = "TRY",
                isVerified = false,
                profilePhotoUrl = (string?)null,
                availabilitySlots = Array.Empty<object>()
            });

            teacherProfileResponse.EnsureSuccessStatusCode();

            var studentResponse = await client.PostAsJsonAsync("/api/students/profiles", new
            {
                userId = (Guid?)null,
                createdByTeacherUserId = teacherUserId,
                parentUserId = (Guid?)null,
                fullName = "Can Deniz",
                gradeLevel = "12. Sinif",
                contactEmail = "veli3@example.com",
                contactPhone = "5554443324",
                goalSummary = "TYT Kimya",
                levelNotes = "Organik kimya",
                origin = 1,
                subjects = Array.Empty<object>()
            });

            studentResponse.EnsureSuccessStatusCode();
            var studentId = (await ReadJsonAsync(studentResponse)).GetProperty("id").GetGuid();

            var startAtUtc = DateTime.UtcNow.AddMinutes(30);
            var endAtUtc = startAtUtc.AddHours(1);
            var lessonResponse = await client.PostAsJsonAsync("/api/scheduling/lessons", new
            {
                teacherUserId,
                studentId,
                subject = "Kimya",
                lessonFormat = 2,
                startAtUtc,
                endAtUtc,
                timeZone = "Europe/Istanbul",
                recurrenceRule = (string?)null,
                reminderOffsetMinutes = 60,
                locationLabel = "Zoom",
                notes = "Hatirlatma testi"
            });

            lessonResponse.EnsureSuccessStatusCode();
            var lessonId = (await ReadJsonAsync(lessonResponse)).GetProperty("id").GetGuid();

            var reminderReady = await WaitForReminderAsync(factory, lessonId);
            Assert.True(reminderReady);

            await using (var dispatchScope = factory.Services.CreateAsyncScope())
            {
                var notificationProcessor = dispatchScope.ServiceProvider.GetRequiredService<INotificationDispatchProcessor>();
                await notificationProcessor.DispatchDueRemindersAsync();
            }

            var remindersResponse = await client.GetAsync($"/api/notifications/teachers/{teacherUserId}/lesson-reminders?activeOnly=false");
            remindersResponse.EnsureSuccessStatusCode();
            var remindersPayload = await ReadJsonAsync(remindersResponse);
            Assert.Single(remindersPayload.EnumerateArray());
            Assert.Equal("Sent", remindersPayload[0].GetProperty("status").GetString());

            await using var scope = factory.Services.CreateAsyncScope();
            var notificationsDbFinal = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            Assert.True(await notificationsDbFinal.OutboxMessages.AnyAsync(message => message.Type == "LessonReminderSentDomainEvent"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", previousConnectionString);
            Environment.SetEnvironmentVariable("Outbox__DispatchEnabled", previousOutboxDispatchEnabled);
            Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", previousOutboxPollInterval);
        }
    }

    [Fact]
    public async Task UpdateTeacherProfile_Should_Not_Change_IsVerified()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "InMemory:isverified-immutable-tests");

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            var registerResponse = await client.PostAsJsonAsync("/api/identity/register", new
            {
                email = "teacher_verify_test@example.com",
                password = "Teacher123!",
                firstName = "Test",
                lastName = "Ogretmen",
                phoneNumber = "5559998877",
                roles = new[] { 2 }
            });
            registerResponse.EnsureSuccessStatusCode();
            var authPayload = await ReadJsonAsync(registerResponse);
            var teacherUserId = authPayload.GetProperty("userId").GetGuid();
            var accessToken = authPayload.GetProperty("accessToken").GetString();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var createResponse = await client.PostAsJsonAsync("/api/teachers/profiles", new
            {
                userId = teacherUserId,
                fullName = "Test Ogretmen",
                subject = "Matematik",
                city = "Istanbul",
                district = "Kadikoy",
                biography = (string?)null,
                headline = (string?)null,
                lessonFormat = 2,
                experienceYears = 3,
                educationLevel = "Lisans",
                hourlyRateAmount = 1000,
                currency = "TRY",
                profilePhotoUrl = (string?)null,
                availabilitySlots = Array.Empty<object>()
            });
            createResponse.EnsureSuccessStatusCode();
            var createPayload = await ReadJsonAsync(createResponse);
            Assert.False(createPayload.GetProperty("isVerified").GetBoolean());

            // isVerified = true gönderilse bile görmezden gelinmeli
            var updateResponse = await client.PutAsJsonAsync($"/api/teachers/profiles/{teacherUserId}", new
            {
                userId = teacherUserId,
                fullName = "Test Ogretmen Guncel",
                subject = "Matematik",
                city = "Istanbul",
                district = "Kadikoy",
                biography = (string?)null,
                headline = (string?)null,
                lessonFormat = 2,
                experienceYears = 4,
                educationLevel = "Lisans",
                hourlyRateAmount = 1100,
                currency = "TRY",
                isVerified = true,
                profilePhotoUrl = (string?)null,
                availabilitySlots = Array.Empty<object>()
            });
            updateResponse.EnsureSuccessStatusCode();
            var updatePayload = await ReadJsonAsync(updateResponse);
            Assert.False(updatePayload.GetProperty("isVerified").GetBoolean());

            var getResponse = await client.GetAsync($"/api/teachers/profiles/{teacherUserId}");
            getResponse.EnsureSuccessStatusCode();
            var getPayload = await ReadJsonAsync(getResponse);
            Assert.False(getPayload.GetProperty("isVerified").GetBoolean());
            Assert.Equal("Test Ogretmen Guncel", getPayload.GetProperty("fullName").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", previousConnectionString);
        }
    }

    [Fact]
    public async Task UpdateStudentProfile_Should_Block_Other_Teacher_And_Allow_Owner()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "InMemory:student-ownership-tests");

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var clientA = factory.CreateClient();
            using var clientB = factory.CreateClient();

            // Teacher A: register + login
            var registerA = await clientA.PostAsJsonAsync("/api/identity/register", new
            {
                email = "teacher-a@ownership.test",
                password = "Teacher123!",
                firstName = "Ali",
                lastName = "A",
                phoneNumber = "5550000001",
                roles = new[] { 2 }
            });
            registerA.EnsureSuccessStatusCode();
            var authA = await ReadJsonAsync(registerA);
            var teacherAUserId = authA.GetProperty("userId").GetGuid();
            var tokenA = authA.GetProperty("accessToken").GetString();
            clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

            // Teacher B: register + login
            var registerB = await clientB.PostAsJsonAsync("/api/identity/register", new
            {
                email = "teacher-b@ownership.test",
                password = "Teacher123!",
                firstName = "Buse",
                lastName = "B",
                phoneNumber = "5550000002",
                roles = new[] { 2 }
            });
            registerB.EnsureSuccessStatusCode();
            var authB = await ReadJsonAsync(registerB);
            var tokenB = authB.GetProperty("accessToken").GetString();
            clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

            // Teacher A creates a student
            var createStudentResponse = await clientA.PostAsJsonAsync("/api/students/profiles", new
            {
                userId = (Guid?)null,
                createdByTeacherUserId = teacherAUserId,
                parentUserId = (Guid?)null,
                fullName = "Mert Demir",
                gradeLevel = "10. Sinif",
                contactEmail = (string?)null,
                contactPhone = (string?)null,
                goalSummary = "YKS hazirligi",
                levelNotes = (string?)null,
                origin = 1,
                subjects = new[] { new { subject = "Matematik", targetLevel = "TYT" } }
            });
            createStudentResponse.EnsureSuccessStatusCode();
            var studentPayload = await ReadJsonAsync(createStudentResponse);
            var studentId = studentPayload.GetProperty("id").GetGuid();
            Assert.True(studentPayload.GetProperty("isActive").GetBoolean());

            // Teacher B tries to update Teacher A's student → 403
            var forbiddenResponse = await clientB.PutAsJsonAsync($"/api/students/profiles/{studentId}", new
            {
                fullName = "Mert Demir (B hack)",
                gradeLevel = "10. Sinif",
                contactEmail = (string?)null,
                contactPhone = (string?)null,
                goalSummary = (string?)null,
                levelNotes = (string?)null,
                isActive = true,
                subjects = Array.Empty<object>()
            });
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

            // Teacher A updates their own student → 200; fullName and isActive change persisted
            var updateResponse = await clientA.PutAsJsonAsync($"/api/students/profiles/{studentId}", new
            {
                fullName = "Mert Demir Guncel",
                gradeLevel = "10. Sinif",
                contactEmail = (string?)null,
                contactPhone = (string?)null,
                goalSummary = "YKS Matematik odakli",
                levelNotes = "Cebir guclendirilecek",
                isActive = false,
                subjects = new[] { new { subject = "Matematik", targetLevel = "TYT 2027" } }
            });
            updateResponse.EnsureSuccessStatusCode();
            var updatedPayload = await ReadJsonAsync(updateResponse);
            Assert.Equal("Mert Demir Guncel", updatedPayload.GetProperty("fullName").GetString());
            Assert.False(updatedPayload.GetProperty("isActive").GetBoolean());
            Assert.Equal("YKS Matematik odakli", updatedPayload.GetProperty("goalSummary").GetString());

            // GET confirms persistence
            var getResponse = await clientA.GetAsync($"/api/students/profiles/{studentId}");
            getResponse.EnsureSuccessStatusCode();
            var getPayload = await ReadJsonAsync(getResponse);
            Assert.Equal("Mert Demir Guncel", getPayload.GetProperty("fullName").GetString());
            Assert.False(getPayload.GetProperty("isActive").GetBoolean());

            // Teacher A reactivates the student
            var reactivateResponse = await clientA.PutAsJsonAsync($"/api/students/profiles/{studentId}", new
            {
                fullName = "Mert Demir Guncel",
                gradeLevel = "10. Sinif",
                contactEmail = (string?)null,
                contactPhone = (string?)null,
                goalSummary = "YKS Matematik odakli",
                levelNotes = "Cebir guclendirilecek",
                isActive = true,
                subjects = Array.Empty<object>()
            });
            reactivateResponse.EnsureSuccessStatusCode();
            var reactivatedPayload = await ReadJsonAsync(reactivateResponse);
            Assert.True(reactivatedPayload.GetProperty("isActive").GetBoolean());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", previousConnectionString);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json;
    }

    private static async Task<bool> WaitForReminderAsync(WebApplicationFactory<Program> factory, Guid lessonId, int attempts = 10)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            await using var dispatchScope = factory.Services.CreateAsyncScope();
            var outboxProcessor = dispatchScope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            await outboxProcessor.DispatchPendingAsync();

            var notificationsDb = dispatchScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            if (await notificationsDb.LessonReminders.AnyAsync(reminder => reminder.LessonScheduleId == lessonId))
            {
                return true;
            }
        }

        return false;
    }
}
