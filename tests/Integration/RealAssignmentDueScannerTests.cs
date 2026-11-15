using System.Data.Common;
using System.Text.Json;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealAssignmentDueScannerTests(ContainerFixture fixture)
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ILocalCalendar Calendar = new LocalCalendar("Europe/Istanbul", new FixedClock(Now));
    private static readonly DateTime Deadline = new DateTime(2026, 11, 15, 21, 0, 0, DateTimeKind.Utc).AddTicks(-10);

    [SkippableFact]
    public async Task Both_Queues_Drain_Despite_250_Expired_Already_Missed_And_250_Unnotified_Assignments()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        await using var database = await TestDatabase.Create(fixture.PostgresConnectionString);
        var db = database.Db;
        for (var i = 0; i < 250; i++)
        {
            var skipped = New(Now.AddDays(-10));
            skipped.MarkMissed(Now.AddDays(-5), Calendar);
            db.Assignments.Add(skipped);
            db.Assignments.Add(New(Now.AddDays(-3)));
            db.Assignments.Add(New(Now.AddHours(1)));
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        database.Reads.Commands.Clear();
        var first = await Scan(db, Now);
        Assert.Equal(new AssignmentDueScanResult(200, 200), first);
        // Partiler veritabanında sınırlanır; teslim geçmişi taramaya yüklenmez.
        Assert.Equal(2, database.Reads.Commands.Count);
        Assert.All(database.Reads.Commands, sql => Assert.DoesNotContain("assignment_submission_versions", sql));
        db.ChangeTracker.Clear();
        Assert.Equal(new AssignmentDueScanResult(50, 50), await Scan(db, Now));
        db.ChangeTracker.Clear();
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, Now));
        Assert.Equal(250, await db.Assignments.CountAsync(a => a.DueSoonNotifiedOnUtc != null));
        Assert.Equal(500, await db.Assignments.CountAsync(a => a.MissedNotifiedOnUtc != null));
        Assert.Equal(250, await db.OutboxMessages.CountAsync(m => m.Type == nameof(AssignmentDueSoonDomainEvent)));
        Assert.Equal(500, await db.OutboxMessages.CountAsync(m => m.Type == nameof(AssignmentMissedDomainEvent)));
        var envelope = await db.OutboxMessages.FirstAsync(m => m.Type == nameof(AssignmentDueSoonDomainEvent));
        var integrationEvent = JsonSerializer.Deserialize<IntegrationEvent>(envelope.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var payload = JsonSerializer.Deserialize<AssignmentDueSoonDomainEvent>(integrationEvent.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(Deadline, payload.DueDateUtc);
    }

    [SkippableFact]
    public async Task Persisted_OnTime_Submission_Returned_After_Deadline_Never_Raises_Missed()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        await using var database = await TestDatabase.Create(fixture.PostgresConnectionString);
        var db = database.Db;
        var returned = New(Now.AddDays(-1));
        Assert.True(returned.SubmitWork("https://example.test/solution", Now.AddDays(-2), Calendar).IsSuccess);
        Assert.False(returned.IsLate);
        Assert.True(returned.ReturnForRevision("Bir soruyu düzelt", Now).IsSuccess);
        var today = New(Now.AddHours(-5)); // Ham UTC anı geçmiş, yerel gün sonu henüz gelmedi.
        db.Assignments.AddRange(returned, today);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(new AssignmentDueScanResult(1, 0), await Scan(db, Now));
        db.ChangeTracker.Clear();
        var before = await db.Assignments.SingleAsync(a => a.Id == returned.Id);
        Assert.Equal(AssignmentStatus.ReturnedForRevision, before.Status);
        Assert.NotNull(before.SubmittedOnUtc);
        Assert.Null(before.MissedNotifiedOnUtc);
        Assert.Single(before.SubmissionVersions);
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, Deadline.AddTicks(-10)));
        Assert.Equal(new AssignmentDueScanResult(0, 1), await Scan(db, Deadline));
        db.ChangeTracker.Clear();
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, Deadline.AddTicks(10)));
        Assert.False(await db.OutboxMessages.AnyAsync(m => m.Type == nameof(AssignmentMissedDomainEvent)
            && m.Payload.Contains(returned.Id.ToString())));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.Type == nameof(AssignmentMissedDomainEvent)));
    }

    [SkippableFact]
    public async Task Scan_Indexes_Upgrade_Downgrade_Upgrade_Keep_Assignment_And_Existing_Warning_Stamps()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli.");
        await using var database = await TestDatabase.Create(fixture.PostgresConnectionString);
        var db = database.Db;
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var target = Assert.Single(migrations, m => m.EndsWith("_AssignmentDueScanIndexes", StringComparison.Ordinal));
        var previous = migrations[Array.IndexOf(migrations, target) - 1];
        var assignment = New(Now.AddDays(-2));
        assignment.MarkMissed(Now, Calendar);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();
        var migrator = db.GetService<IMigrator>();
        Assert.Equal(2, await IndexCount(database.ConnectionString));
        await migrator.MigrateAsync(previous);
        Assert.Equal(0, await IndexCount(database.ConnectionString));
        await migrator.MigrateAsync(target);
        Assert.Equal(2, await IndexCount(database.ConnectionString));
        db.ChangeTracker.Clear();
        var stored = await db.Assignments.SingleAsync(a => a.Id == assignment.Id);
        Assert.Equal(Now, stored.MissedNotifiedOnUtc);
        Assert.Null(stored.DueSoonNotifiedOnUtc);
        Assert.Equal(new AssignmentDueScanResult(0, 0), await Scan(db, Now));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.Type == nameof(AssignmentMissedDomainEvent)));
    }

    private static async Task<int> IndexCount(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*)::int FROM pg_indexes WHERE schemaname='assignments' AND indexname IN ('IX_assignments_due_soon_scan', 'IX_assignments_missed_scan')", connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static Task<AssignmentDueScanResult> Scan(AssignmentsDbContext db, DateTime now)
        => new AssignmentDueScanProcessor(db, new FixedClock(now), Calendar).ScanAsync();

    private static Assignment New(DateTime due) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
        "Tarama ödevi", null, due, AssignmentStatus.Pending, null, Now.AddDays(-20), null);

    private sealed record FixedClock(DateTime UtcNow) : IClock;

    private sealed class ReadCommands : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)) Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    // Her test yalnız kendi geçici veritabanını tarar; diğer senaryoların bekleyen ödev/olaylarını tüketmez.
    private sealed class TestDatabase(string adminConnection, string name, string connectionString,
        AssignmentsDbContext db, ReadCommands reads) : IAsyncDisposable
    {
        public string ConnectionString { get; } = connectionString;
        public AssignmentsDbContext Db { get; } = db;
        public ReadCommands Reads { get; } = reads;

        public static async Task<TestDatabase> Create(string connectionString)
        {
            var name = "assignment_due_" + Guid.NewGuid().ToString("N");
            await using var admin = new NpgsqlConnection(connectionString);
            await admin.OpenAsync();
            await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin)) await create.ExecuteNonQueryAsync();
            var isolated = new NpgsqlConnectionStringBuilder(connectionString) { Database = name, Pooling = false }.ConnectionString;
            var reads = new ReadCommands();
            var db = new AssignmentsDbContext(new DbContextOptionsBuilder<AssignmentsDbContext>()
                .UseNpgsql(isolated).AddInterceptors(reads).Options, new JsonDomainEventMapper());
            var database = new TestDatabase(connectionString, name, isolated, db, reads);
            try { await db.Database.MigrateAsync(); return database; }
            catch { await database.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name}", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
