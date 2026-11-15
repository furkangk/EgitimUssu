using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Infrastructure.Storage;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealAssignmentSubmissionTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Http_revision_resubmission_approval_keeps_versions_files_and_grade_and_enforces_ownership()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var env = RealInfrastructure.Use(fixture);
        var root = Path.Combine(Path.GetTempPath(), $"assignment-{Guid.NewGuid():N}");
        await using var factory = Factory(root);
        try
        {
            using var teacher = factory.CreateClient();
            var teacherId = await Login(teacher, 2, "10.194.1.1");
            using var student = factory.CreateClient();
            var studentId = await Login(student, 3, "10.194.1.2");
            using var stranger = factory.CreateClient();
            await Login(stranger, 2, "10.194.1.3");
            var id = await Seed(factory, teacherId, studentId);
            using var premature = await teacher.PostAsJsonAsync($"/api/assignments/{id}/approve", new { feedback = "erken" });
            await Invalid(premature);
            using var upload = await Upload(student, id, "ilk");
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            Assert.True((await Json(upload)).GetProperty("isLate").GetBoolean());
            using var forbidden = await stranger.PostAsJsonAsync($"/api/assignments/{id}/approve", new { });
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            using var foreignUpload = await Upload(stranger, id, "saldırı");
            Assert.Equal(HttpStatusCode.Forbidden, foreignUpload.StatusCode);
            using var returned = await teacher.PostAsJsonAsync($"/api/assignments/{id}/return", new { feedback = "ikinci soru eksik" });
            Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
            Assert.Equal(JsonValueKind.Null, (await Json(returned)).GetProperty("score").ValueKind);
            using var cannotComplete = await student.PostAsync($"/api/assignments/{id}/complete", null);
            await Invalid(cannotComplete);
            using var second = await Upload(student, id, "ikinci");
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            using var approved = await teacher.PostAsJsonAsync($"/api/assignments/{id}/approve", new { feedback = "tamam" });
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            using var grade = await teacher.PostAsJsonAsync($"/api/assignments/{id}/grade", new { score = 92, feedback = "çok iyi" });
            Assert.Equal(HttpStatusCode.OK, grade.StatusCode);
            var versions = (await Json(grade)).GetProperty("submissionVersions");
            Assert.Equal(2, versions.GetArrayLength());
            Assert.Equal("ikinci soru eksik", versions[0].GetProperty("teacherFeedback").GetString());
            Assert.Equal(92, versions[1].GetProperty("score").GetInt32());
            using var denied = await Upload(student, id, "üçüncü");
            await Invalid(denied);
            using var terminalReturn = await teacher.PostAsJsonAsync($"/api/assignments/{id}/return", new { feedback = "yeniden" });
            await Invalid(terminalReturn);
            using var download = await teacher.GetAsync($"/api/assignments/{id}/attachment?version=1");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("ilk", await download.Content.ReadAsStringAsync());
            using var latest = await student.GetAsync($"/api/assignments/{id}/attachment");
            Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
            Assert.Equal("ikinci", await latest.Content.ReadAsStringAsync());
            using var foreignDownload = await stranger.GetAsync($"/api/assignments/{id}/attachment?version=1");
            Assert.Equal(HttpStatusCode.Forbidden, foreignDownload.StatusCode);
            using var missing = await student.GetAsync($"/api/assignments/{id}/attachment?version=99");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(2, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
            var stored = await db.Assignments.SingleAsync(x => x.Id == id);
            Assert.Equal(92, stored.Score);
            Assert.Equal(2, stored.SubmissionVersions.Count);
            Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.Type == nameof(AssignmentSubmittedDomainEvent) && x.Payload.Contains(id.ToString())));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [SkippableFact]
    public async Task Concurrent_upload_loser_has_no_blob_version_or_event_and_does_not_overwrite_winner()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var env = RealInfrastructure.Use(fixture);
        var root = Path.Combine(Path.GetTempPath(), $"assignment-race-{Guid.NewGuid():N}");
        var barrier = new UploadBarrier();
        await using var factory = Factory(root, barrier);
        try
        {
            using var student = factory.CreateClient();
            var user = await Login(student, 3, "10.194.2.1");
            var id = await Seed(factory, Guid.NewGuid(), user);
            barrier.AssignmentId = id;
            var responses = await Task.WhenAll(Upload(student, id, "bir"), Upload(student, id, "iki"));
            try
            {
                Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
                var loser = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
                Assert.Equal("shared.concurrency_conflict", (await Json(loser)).GetProperty("code").GetString());
                Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
                await using var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
                var a = await db.Assignments.SingleAsync(x => x.Id == id);
                Assert.Single(a.SubmissionVersions);
                Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.Type == nameof(AssignmentSubmittedDomainEvent) && x.Payload.Contains(id.ToString())));
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [SkippableFact]
    public async Task Anonymization_removes_all_version_blobs_and_feedback_and_is_repeatable()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        using var env = RealInfrastructure.Use(fixture);
        var root = Path.Combine(Path.GetTempPath(), $"assignment-delete-{Guid.NewGuid():N}");
        await using var factory = Factory(root);
        try
        {
            using var student = factory.CreateClient();
            var user = await Login(student, 3, "10.194.3.1");
            var id = await Seed(factory, Guid.NewGuid(), user);
            using var first = await Upload(student, id, "eski kişisel");
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            using var second = await Upload(student, id, "yeni kişisel");
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
            var a = await db.Assignments.SingleAsync(x => x.Id == id);
            a.ReturnForRevision("kişisel geri bildirim", DateTime.UtcNow);
            await db.SaveChangesAsync();
            var anonymizer = new AssignmentsPersonalDataAnonymizer(db, scope.ServiceProvider.GetRequiredService<IFileStorage>());
            Assert.Equal(1, await anonymizer.AnonymizeAsync(new PersonalDataSubject(user, [user]), default));
            Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            db.ChangeTracker.Clear();
            a = await db.Assignments.SingleAsync(x => x.Id == id);
            Assert.Equal(2, a.SubmissionVersions.Count);
            Assert.All(a.SubmissionVersions, x => { Assert.Null(x.StorageKey); Assert.Null(x.FileName); Assert.Null(x.AttachmentUrl); Assert.Null(x.TeacherFeedback); });
            Assert.Equal(0, await anonymizer.AnonymizeAsync(new PersonalDataSubject(user, [user]), default));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [SkippableFact]
    public async Task Migration_backfills_version_one_from_last_event_or_honest_fallback_and_down_up_keeps_latest_file()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = $"assignment_migration_{Guid.NewGuid():N}" };
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new AssignmentsDbContext(new DbContextOptionsBuilder<AssignmentsDbContext>().UseNpgsql(connection.ConnectionString,
                o => o.MigrationsHistoryTable("__ef_migrations_history", "assignments")).Options, new JsonDomainEventMapper());
            var all = db.Database.GetMigrations().ToArray();
            var index = Array.FindIndex(all, x => x.EndsWith("_AssignmentStateAndSubmissions", StringComparison.Ordinal));
            Assert.True(index > 0);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(all[index - 1]);
            var now = new DateTime(2026, 11, 15, 21, 0, 0, DateTimeKind.Utc);
            var ids = new List<Guid>();
            foreach (var status in new[] { "Pending", "InProgress", "Completed", "Approved", "ReturnedForRevision", "Cancelled" })
            {
                var id = Guid.NewGuid(); ids.Add(id);
                var created = now.AddDays(-2);
                var completed = status == "Completed" ? now.AddMinutes(-1) : (DateTime?)null;
                var key = status is "InProgress" or "Approved" or "ReturnedForRevision" ? $"legacy/{id}" : null;
                var attachment = key is null ? "https://teacher.example/task" : $"/api/assignments/{id}/attachment";
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO assignments.assignments ("Id","StudentId","TeacherUserId","Title","Status","CreatedOnUtc","CompletedOnUtc","DueDateUtc","AttachmentUrl","AttachmentStorageKey","AttachmentFileName","Score","TeacherFeedback")
                    VALUES ({id},{Guid.NewGuid()},{Guid.NewGuid()},'Eski ödev',{status},{created},{completed},{now.AddHours(-21)},{attachment},{key},'eski.pdf',80,'eski yorum')
                    """);
                if (status == "InProgress")
                {
                    foreach (var time in new[] { now.AddMinutes(-1), now })
                    {
                        var domainEvent = new AssignmentSubmittedDomainEvent(id, Guid.NewGuid(), Guid.NewGuid(), null, time);
                        var envelope = Assert.Single(new JsonDomainEventMapper().Map("Assignments", domainEvent));
                        db.OutboxMessages.Add(new OutboxMessage { Id = envelope.EventId, Module = "Assignments", Type = envelope.Name,
                            Payload = JsonSerializer.Serialize(envelope, envelope.GetType(), IntegrationEventSerialization.Options), OccurredOnUtc = time });
                    }
                }
            }
            await db.SaveChangesAsync();
            await migrator.MigrateAsync();
            var rows = await db.Assignments.OrderBy(x => x.Status).ToArrayAsync();
            Assert.Equal(4, rows.Sum(x => x.SubmissionVersions.Count));
            Assert.All(rows.Where(x => x.Status is AssignmentStatus.Pending or AssignmentStatus.Cancelled), x => { Assert.Null(x.SubmittedOnUtc); Assert.Empty(x.SubmissionVersions); });
            var progress = rows.Single(x => x.Status == AssignmentStatus.InProgress);
            Assert.Equal(now, progress.SubmittedOnUtc);
            Assert.True(progress.IsLate);
            Assert.False(rows.Single(x => x.Status == AssignmentStatus.Completed).IsLate);
            var approved = rows.Single(x => x.Status == AssignmentStatus.Approved);
            Assert.Equal(now.AddDays(-2), approved.SubmittedOnUtc);
            Assert.Equal(80, Assert.Single(approved.SubmissionVersions).Score);
            var returned = rows.Single(x => x.Status == AssignmentStatus.ReturnedForRevision);
            Assert.Null(returned.Score);
            Assert.Equal(80, Assert.Single(returned.SubmissionVersions).Score);
            db.ChangeTracker.Clear();
            await migrator.MigrateAsync(all[index - 1]);
            await migrator.MigrateAsync();
            var reloaded = await db.Assignments.SingleAsync(x => x.Id == approved.Id);
            Assert.Equal(approved.AttachmentStorageKey, Assert.Single(reloaded.SubmissionVersions).StorageKey);
            Assert.Equal(6, await db.Assignments.CountAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static WebApplicationFactory<Program> Factory(string root, UploadBarrier? barrier = null) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IFileStorage>();
            s.AddSingleton<IFileStorage>(new LocalFileStorage(Options.Create(new StorageOptions { LocalRootPath = root })));
            if (barrier is not null) s.AddDbContext<AssignmentsDbContext>(o => o.AddInterceptors(barrier));
        }));

    private static async Task<Guid> Seed(WebApplicationFactory<Program> factory, Guid teacher, Guid student)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
        var id = Guid.NewGuid();
        db.Assignments.Add(new Assignment(id, student, teacher, null, "Ödev", null, DateTime.UtcNow.AddDays(-1), AssignmentStatus.Pending, null, DateTime.UtcNow, null));
        await db.SaveChangesAsync();
        return id;
    }
    private static async Task<Guid> Login(HttpClient client, int role, string ip)
    {
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        using var response = await client.PostAsJsonAsync("/api/identity/register", new { email = $"assignment-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Ödev", lastName = "Test", roles = new[] { role } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return body.GetProperty("userId").GetGuid();
    }
    private static Task<HttpResponseMessage> Upload(HttpClient client, Guid id, string text)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(text)) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } }, "file", "odev.pdf");
        return Send();
        async Task<HttpResponseMessage> Send() { using (form) return await client.PostAsync($"/api/assignments/{id}/submission", form); }
    }
    private static async Task Invalid(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("assignments.invalid_transition", (await Json(response)).GetProperty("code").GetString());
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }
    private sealed class UploadBarrier : SaveChangesInterceptor
    {
        public Guid AssignmentId { get; set; }
        private int _arrivals;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (AssignmentId != Guid.Empty && eventData.Context!.ChangeTracker.Entries<Assignment>().Any(x => x.Entity.Id == AssignmentId && x.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return result;
        }
    }
}
