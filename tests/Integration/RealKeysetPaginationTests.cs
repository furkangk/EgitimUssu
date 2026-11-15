using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EgitimUssu.Shared.Infrastructure.Persistence;
using System.Data.Common;
using System.Diagnostics;
using EgitimUssu.Modules.ProgressTracking.Domain;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EgitimUssu.Modules.Assignments.Application;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Modules.Notifications.Application;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealKeysetPaginationTests(ContainerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    [SkippableFact]
    public async Task Five_real_lists_seek_equal_times_without_duplicates_and_keep_filters()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var study = services.GetRequiredService<StudyDbContext>();
        var assignments = services.GetRequiredService<AssignmentsDbContext>();
        var notifications = services.GetRequiredService<NotificationsDbContext>();
        var student = Guid.NewGuid(); var foreign = Guid.NewGuid(); var teacher = Guid.NewGuid();
        for (var i = 0; i < 106; i++)
        {
            var sid = i == 105 ? foreign : student;
            var session = StudySession.CreateManual(Guid.NewGuid(), sid, "Mat", null, 20, Now, null, false, false, Now);
            session.ClearDomainEvents(); study.StudySessions.Add(session);
            var test = new TestResult(Guid.NewGuid(), sid, "Mat", null, null, TestType.Topic, 10, 8, 0, 2, 4, null, Now, false, false, Now);
            test.ClearDomainEvents(); study.TestResults.Add(test);
            study.StudyNotes.Add(new StudyNote(Guid.NewGuid(), sid, "Not", "İçerik", null, null, null, Now));
            var assignment = new Assignment(Guid.NewGuid(), sid, teacher, null, "Ödev", null, null, AssignmentStatus.Pending, null, Now, null);
            assignment.ClearDomainEvents(); assignments.Assignments.Add(assignment);
            notifications.UserNotifications.Add(new UserNotification(Guid.NewGuid(), sid, UserNotificationKind.System, "Başlık", "İçerik", null, Now));
        }
        await study.SaveChangesAsync(); await assignments.SaveChangesAsync(); await notifications.SaveChangesAsync();
        study.ChangeTracker.Clear(); assignments.ChangeTracker.Clear(); notifications.ChangeTracker.Clear();
        var sr = services.GetRequiredService<IStudyRepository>();
        var ar = services.GetRequiredService<IAssignmentRepository>();
        var nr = services.GetRequiredService<IUserNotificationRepository>();
        await Check(async p => (await sr.ListSessionsAsync(student, null, null, "Mat", default, p)).Select(x => (x.Id, x.StartedAtUtc)).ToArray());
        await Check(async p => (await sr.ListTestsAsync(student, "Mat", null, null, null, default, p)).Select(x => (x.Id, x.TakenOnUtc)).ToArray());
        await Check(async p => (await sr.ListNotesAsync(student, default, p)).Select(x => (x.Id, x.CreatedOnUtc)).ToArray());
        await Check(async p => (await ar.ListAsync(teacher, student, null, default, p)).Select(x => (x.Id, x.CreatedOnUtc)).ToArray());
        await Check(async p => (await nr.ListAsync(student, false, 0, 50, default, p)).Items.Select(x => (x.Id, x.CreatedOnUtc)).ToArray());
        Assert.Empty(await sr.ListTestsAsync(student, "Fizik", null, null, null, default, new(50)));
        Assert.Empty(await ar.ListAsync(foreign, student, null, default, new(50)));
        Assert.Empty(study.ChangeTracker.Entries());
        Assert.Empty(assignments.ChangeTracker.Entries());
        Assert.Empty(notifications.ChangeTracker.Entries());
    }

    [SkippableFact]
    public async Task Snapshot_batches_205_students_and_does_not_retain_written_entities()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var reads = new Reads();
        await using var db = new ProgressTrackingDbContext(new DbContextOptionsBuilder<ProgressTrackingDbContext>()
            .UseNpgsql(fixture.PostgresConnectionString).AddInterceptors(reads).Options, new NoOpDomainEventMapper());
        var students = Enumerable.Range(0, 205).Select(_ => Guid.NewGuid()).ToArray();
        try
        {
            foreach (var student in students)
                db.TopicMasteries.Add(new TopicMastery(Guid.NewGuid(), student, "Batch", "Topic", Now));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear(); reads.Commands.Clear();
            var clock = new FixedClock();
            var processor = new ProgressSnapshotProcessor(db, clock, new GuidIdGenerator(), new LocalCalendar("Europe/Istanbul", clock));
            await processor.RunAsync();
            Assert.Equal(410, await db.ProgressSnapshots.CountAsync(x => students.Contains(x.StudentId)));
            Assert.Empty(db.ChangeTracker.Entries<ProgressSnapshot>());
            Assert.True(reads.Commands.Count(x => x.Contains("GROUP BY", StringComparison.Ordinal)) >= 4);
            Assert.All(reads.Commands.Where(x => x.Contains("GROUP BY", StringComparison.Ordinal)), x => Assert.Contains("WHERE", x));
            Assert.Equal(0, await processor.RunAsync());
        }
        finally
        {
            await db.ProgressSnapshots.Where(x => students.Contains(x.StudentId)).ExecuteDeleteAsync();
            await db.TopicMasteries.Where(x => students.Contains(x.StudentId)).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Http_preserves_legacy_arrays_and_enforces_cursor_bounds_and_ownership()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var infra = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var owner = await Register(factory, 3); using var client = owner.Client;
        var teacher = await Register(factory, 2); using var teacherClient = teacher.Client;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var study = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            var assignments = scope.ServiceProvider.GetRequiredService<AssignmentsDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            study.StudyStudents.Add(new StudyStudent(owner.Id, owner.Id, Now));
            for (var i = 0; i < 105; i++)
            {
                var session = StudySession.CreateManual(Guid.NewGuid(), owner.Id, "Http", null, 20, Now, null, false, false, Now);
                session.ClearDomainEvents(); study.StudySessions.Add(session);
                var test = new TestResult(Guid.NewGuid(), owner.Id, "Http", null, null, TestType.Topic, 10, 8, 0, 2, 4, null, Now, false, false, Now);
                test.ClearDomainEvents(); study.TestResults.Add(test);
                study.StudyNotes.Add(new StudyNote(Guid.NewGuid(), owner.Id, "Not", "İçerik", null, null, null, Now));
                var assignment = new Assignment(Guid.NewGuid(), owner.Id, teacher.Id, null, "Ödev", null, null, AssignmentStatus.Pending, null, Now, null);
                assignment.ClearDomainEvents(); assignments.Assignments.Add(assignment);
                notifications.UserNotifications.Add(new UserNotification(Guid.NewGuid(), owner.Id, UserNotificationKind.System, "Başlık", "İçerik", null, Now));
            }
            var old = StudySession.CreateManual(Guid.NewGuid(), owner.Id, "Old", null, 20, Now.AddDays(-40), null, false, false, Now);
            old.ClearDomainEvents(); study.StudySessions.Add(old);
            await study.SaveChangesAsync(); await assignments.SaveChangesAsync(); await notifications.SaveChangesAsync();
        }
        var paths = new[] { $"/api/study/students/{owner.Id}/sessions", $"/api/study/students/{owner.Id}/test-results",
            $"/api/study/students/{owner.Id}/notes", $"/api/assignments?studentId={owner.Id}" };
        foreach (var path in paths)
        {
            var separator = path.Contains('?') ? "&" : "?";
            var legacy = await Json(await client.GetAsync(path));
            Assert.Equal(JsonValueKind.Array, legacy.ValueKind); Assert.Equal(50, legacy.GetArrayLength());
            var page = await Json(await client.GetAsync(path + separator + "limit=20"));
            Assert.Equal(20, page.GetProperty("items").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, page.GetProperty("totalCount").ValueKind);
            var cursor = page.GetProperty("nextCursor").GetString(); Assert.NotNull(cursor);
            var next = await Json(await client.GetAsync(path + separator + "limit=20&after=" + Uri.EscapeDataString(cursor)));
            Assert.Empty(page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Intersect(
                next.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid())));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + separator + "limit=0")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + separator + "after=broken")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + separator + "limit=101")).StatusCode);
        }
        var notificationPath = $"/api/notifications/users/{owner.Id}/notifications";
        var offset = await Json(await client.GetAsync(notificationPath + "?skip=20&take=20"));
        Assert.Equal(105, offset.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, offset.GetProperty("page").GetInt32());
        var notificationPage = await Json(await client.GetAsync(notificationPath + "?limit=50"));
        Assert.Equal(50, notificationPage.GetProperty("items").GetArrayLength());
        var dashboard = await Json(await teacherClient.GetAsync($"/api/teachers/profiles/{teacher.Id}/dashboard-summary"));
        Assert.Equal(105, dashboard.GetProperty("pendingAssignments").GetArrayLength());
        var oldPage = await Json(await client.GetAsync(paths[0] + "?subject=Old&limit=50"));
        Assert.Equal(0, oldPage.GetProperty("items").GetArrayLength());
        foreach (var role in new[] { 2, 3, 4 })
        {
            var other = await Register(factory, role); using var foreign = other.Client;
            foreach (var path in paths.Append(notificationPath))
            {
                var scoped = path.StartsWith("/api/assignments", StringComparison.Ordinal) ? path + $"&teacherUserId={teacher.Id}" : path;
                var response = await foreign.GetAsync(scoped + (scoped.Contains('?') ? "&" : "?") + "limit=50");
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"role={role}, path={scoped}, status={response.StatusCode}");
            }
        }
    }

    [SkippableFact]
    public async Task Source_reader_scale_and_index_migrations_are_measured_on_isolated_postgres()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        var name = "keyset_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(fixture.PostgresConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin)) await create.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(fixture.PostgresConnectionString) { Database = name, Pooling = false }.ConnectionString;
        try
        {
            var reads = new Reads();
            await using var study = new StudyDbContext(new DbContextOptionsBuilder<StudyDbContext>()
                .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations_history", "study")).AddInterceptors(reads).Options, new NoOpDomainEventMapper());
            await using var assignments = new AssignmentsDbContext(new DbContextOptionsBuilder<AssignmentsDbContext>()
                .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations_history", "assignments")).Options, new NoOpDomainEventMapper());
            await using var notifications = new NotificationsDbContext(new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations_history", "notifications")).Options, new NoOpDomainEventMapper());
            foreach (var db in new DbContext[] { study, assignments, notifications })
            {
                await db.Database.MigrateAsync();
                var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
                Assert.EndsWith("_ListKeysetIndexes", migrations[^1]);
                await db.GetService<IMigrator>().MigrateAsync(migrations[^2]);
                await db.Database.MigrateAsync();
            }
            var student = Guid.NewGuid();
            for (var i = 0; i < 5000; i++)
            {
                var session = StudySession.CreateManual(Guid.NewGuid(), student, "Scale", "Topic", 20, Now.AddMinutes(-i), null, false, false, Now);
                session.ClearDomainEvents(); study.StudySessions.Add(session);
                var test = new TestResult(Guid.NewGuid(), student, "Scale", "Topic", null, TestType.Topic, 10, 8, 0, 2, 4, null, Now.AddMinutes(-i), false, false, Now);
                test.ClearDomainEvents(); study.TestResults.Add(test);
            }
            await study.SaveChangesAsync(); study.ChangeTracker.Clear(); reads.Commands.Clear();
            var allocated = GC.GetTotalAllocatedBytes(); var timer = Stopwatch.StartNew();
            var result = await new StudyStatisticsReader(study).ReadAsync(student, default);
            timer.Stop();
            Console.WriteLine($"STUDY_SCALE sessions={result.Sessions.Count} tests={result.Tests.Count} selects={reads.Commands.Count} elapsed_ms={timer.ElapsedMilliseconds} allocated_bytes={GC.GetTotalAllocatedBytes() - allocated}");
            Assert.Equal(5000, result.Sessions.Count); Assert.Equal(5000, result.Tests.Count); Assert.Equal(2, reads.Commands.Count);
            Assert.Empty(study.ChangeTracker.Entries());
            var sql = study.TestResults.Where(x => x.StudentId == student).Seek(new(50, Now.AddMinutes(-4500), Guid.NewGuid()), x => x.TakenOnUtc, x => x.Id, rowComparison: true).ToQueryString();
            Assert.Contains(") < (", sql);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<(HttpClient Client, Guid Id)> Register(WebApplicationFactory<Program> factory, int role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.176.{Random.Shared.Next(1,254)}.{Random.Shared.Next(1,254)}");
        var auth = await Json(await client.PostAsJsonAsync("/api/identity/register", new
            { email = $"page-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Page", lastName = "Test", roles = new[] { role } }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return (client, auth.GetProperty("userId").GetGuid());
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => Now; }
    private sealed class Reads : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)) Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private static async Task Check(Func<KeysetPage, Task<(Guid Id, DateTime Time)[]>> read)
    {
        var ids = new HashSet<Guid>();
        var request = new KeysetPage(50);
        var pageCount = 0;
        while (true)
        {
            var rows = await read(request);
            Assert.InRange(rows.Length, 0, 51);
            var page = request.Finish(rows, x => x.Time, x => x.Id);
            foreach (var row in page.Items) Assert.True(ids.Add(row.Id), "Sayfalarda tekrar var.");
            pageCount++;
            if (page.NextCursor is null) break;
            Assert.True(KeysetPage.TryCreate(page.NextCursor, 50, out request));
            Assert.InRange(pageCount, 1, 3);
        }
        Assert.Equal(105, ids.Count);
        Assert.Equal(3, pageCount);
    }
}
