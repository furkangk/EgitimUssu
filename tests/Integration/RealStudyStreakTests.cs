using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace EgitimUssu.Tests.Integration;

[Collection("containers")]
public sealed class RealStudyStreakTests(ContainerFixture fixture, ITestOutputHelper output)
{
    [SkippableFact]
    public async Task Http_Manual_Stopwatch_Night_And_Backdated_Days_Are_Persisted_Once()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        using var infrastructure = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.197.1.1");
        var auth = await Json(await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"streak-{Guid.NewGuid():N}@test.com", password = "Passw0rd!", firstName = "Seri", lastName = "Test", roles = new[] { 3 }
        }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        var student = Guid.NewGuid();
        try
        {
            var clock = factory.Services.GetRequiredService<IClock>();
            var calendar = factory.Services.GetRequiredService<ILocalCalendar>();
            var today = calendar.StudyDay(clock.UtcNow);
            await Json(await client.PutAsJsonAsync($"/api/study/students/{student}/goals", new
            {
                dailyGoalMinutes = 60, streakThresholdPercent = 100
            }));
            await Manual(client, student, clock.UtcNow, 90);
            Assert.Equal(1, (await Streak(client, student)).GetProperty("currentStreakDays").GetInt32());
            var session = await Json(await client.PostAsJsonAsync("/api/study/sessions/start", new { studentId = student, subject = "Mat" }));
            await using (var elapsed = factory.Services.CreateAsyncScope())
            {
                var sessionId = session.GetProperty("id").GetGuid();
                var started = clock.UtcNow.AddMinutes(-90);
                await elapsed.ServiceProvider.GetRequiredService<StudyDbContext>().StudySessions.Where(s => s.Id == sessionId)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.StartedAtUtc, started).SetProperty(s => s.LastResumedAtUtc, started));
            }
            await Json(await client.PostAsJsonAsync($"/api/study/sessions/{session.GetProperty("id").GetGuid()}/complete",
                new { clientEffectiveMinutes = 90 }));
            Assert.Equal(1, (await Streak(client, student)).GetProperty("totalStudyDays").GetInt32());
            // Dün seri günü: bugünün TR 01:30'u. Erken saatlerde de geleceğe giriş olmaz.
            var yesterdayNight = calendar.ToUtc(today, new TimeOnly(1, 30));
            await Manual(client, student, yesterdayNight, 35);
            await Manual(client, student, calendar.ToUtc(today.AddDays(-1), new TimeOnly(12, 0)), 25);
            Assert.Equal(2, (await Streak(client, student)).GetProperty("currentStreakDays").GetInt32());
            await Manual(client, student, calendar.ToUtc(today.AddDays(-2), new TimeOnly(12, 0)), 90);
            await Manual(client, student, calendar.ToUtc(today.AddDays(-2), new TimeOnly(13, 0)), 90);
            var final = await Streak(client, student);
            Assert.Equal(3, final.GetProperty("currentStreakDays").GetInt32());
            Assert.Equal(3, final.GetProperty("totalStudyDays").GetInt32());
            await using var read = factory.Services.CreateAsyncScope();
            var db = read.ServiceProvider.GetRequiredService<StudyDbContext>();
            Assert.Equal(6, await db.StudySessions.CountAsync(s => s.StudentId == student && s.Status == StudySessionStatus.Completed));
        }
        finally
        {
            // Bu test taşıma/istatistik senaryosu değildir; sonraki global outbox turlarına kendi
            // bekleyen olaylarını bırakmaz (snapshot testleri tüm öğrencileri tarar).
            await using var cleanup = factory.Services.CreateAsyncScope();
            await cleanup.ServiceProvider.GetRequiredService<StudyDbContext>().OutboxMessages
                .Where(message => message.Payload.Contains(student.ToString())).ExecuteDeleteAsync();
        }
    }

    [SkippableFact]
    public async Task Local_Staging_Demo_Repair_Is_Atomic_And_Startup_Rerun_Does_Not_Write()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");
        // Ayrı ve geçici DB: onarım tüm öğrencileri dolaşır; diğer senaryoların verisine dokunmaz.
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithPassword(Guid.NewGuid().ToString("N")).Build();
        await postgres.StartAsync();
        using var infrastructure = RealInfrastructure.Use(fixture);
        var redisHost = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address).First(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        var redisPort = fixture.RedisConnectionString.Split(':').Last();
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = postgres.GetConnectionString(),
            ["Redis:Configuration"] = $"{redisHost}:{redisPort},abortConnect=false",
            ["Jwt:SigningKey"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ["Email:FromEmail"] = "streak@test.com",
            ["Seeding:Enabled"] = "true",
            ["Study:RepairStreaksOnStartup"] = "false",
            ["Logging:LogLevel:Default"] = "Warning"
        };
        using var stagingEnvironment = new TestEnvironment(settings.ToDictionary(pair => pair.Key.Replace(":", "__"), pair => pair.Value));
        await using (var before = Factory())
        {
            // Program migration'ları ve gerçek Staging komut-seeder'ı çalıştırır.
            _ = before.CreateClient();
            await using var scope = before.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            Assert.Equal(12, await db.StudySessions.CountAsync(s => s.Status == StudySessionStatus.Completed));
            var streaks = await db.StudyStreaks.OrderBy(s => s.StudentId).ToArrayAsync();
            Assert.NotEmpty(streaks);
            output.WriteLine("STAGING seed önce: " + string.Join("; ", streaks.Select(s => $"{s.StudentId}:{s.CurrentStreakDays}/{s.LongestStreakDays}/{s.TotalStudyDays}")));
            // Önceki N-40'ın bozuk sayaçlarını temsil eden kontrollü yerel demo verisi.
            await db.StudyStreaks.ExecuteUpdateAsync(s => s.SetProperty(x => x.CurrentStreakDays, 99)
                .SetProperty(x => x.LongestStreakDays, 99).SetProperty(x => x.TotalStudyDays, 99));
            output.WriteLine("STAGING onarım önce: tüm demo sayaçları 99/99/99.");
            // Son marker yazımı başarısız olsa bile önceki parti kayıtları commit edilmemeli.
            await using var broken = new StudyDbContext(new DbContextOptionsBuilder<StudyDbContext>()
                .UseNpgsql(postgres.GetConnectionString()).AddInterceptors(new FailMarkerSave()).Options,
                new JsonDomainEventMapper());
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => new StudyStreakRepairJob(
                broken, scope.ServiceProvider.GetRequiredService<ILocalCalendar>(),
                scope.ServiceProvider.GetRequiredService<IClock>(), scope.ServiceProvider.GetRequiredService<IIdGenerator>()).RunAsync());
            Assert.Equal(FailMarkerSave.Message, failure.Message);
            db.ChangeTracker.Clear();
            Assert.All(await db.StudyStreaks.AsNoTracking().ToArrayAsync(), s =>
                Assert.Equal((99, 99, 99), (s.CurrentStreakDays, s.LongestStreakDays, s.TotalStudyDays)));
            Assert.Empty(await db.ModuleStates.ToArrayAsync());
            output.WriteLine("STAGING marker hatası: tüm parti yazımları geri alındı; marker yok, sayaçlar 99/99/99.");
        }
        Environment.SetEnvironmentVariable("Study__RepairStreaksOnStartup", "true");
        (Guid, int, int, int, DateTime)[] repaired;
        await using (var after = Factory())
        {
            _ = after.CreateClient();
            await using var scope = after.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            var calendar = scope.ServiceProvider.GetRequiredService<ILocalCalendar>();
            var today = calendar.StudyDay(scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
            var sessions = await db.StudySessions.ToArrayAsync();
            var goals = await db.StudyGoals.Where(g => g.IsActive).ToArrayAsync();
            var streaks = await db.StudyStreaks.OrderBy(s => s.StudentId).ToArrayAsync();
            foreach (var streak in streaks)
            {
                var goal = goals.Where(g => g.StudentId == streak.StudentId).OrderByDescending(g => g.UpdatedOnUtc).FirstOrDefault();
                // Bağımsız beklenen toplam: tamamlanmış seansların 04:00 günleri, hedef eşiğiyle.
                var days = sessions.Where(s => s.StudentId == streak.StudentId && s.Status == StudySessionStatus.Completed)
                    .GroupBy(s => calendar.StudyDay(s.EndedAtUtc!.Value))
                    .Where(g => g.Sum(s => s.EffectiveMinutes) >= StreakRules.EffectiveThresholdMinutes(
                        goal?.DailyGoalMinutes ?? 0, goal?.StreakThresholdPercent ?? 60))
                    .Select(g => g.Key).Distinct().ToArray();
                Assert.Equal(days.Length, streak.TotalStudyDays);
                Assert.Equal(days.Max(), streak.LastStudiedOnDate);
                Assert.True(streak.LongestStreakDays < 99);
                Assert.Equal(streak.LastStudiedOnDate < today.AddDays(-1) ? 0 : streak.CurrentStreakDays, streak.CurrentAt(today));
            }
            output.WriteLine("STAGING onarım sonra: " + string.Join("; ", streaks.Select(s => $"{s.StudentId}:{s.CurrentStreakDays}/{s.LongestStreakDays}/{s.TotalStudyDays}")));
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<StudyStreakRepairJob>().RunAsync());
            repaired = streaks.Select(s => (s.Id, s.CurrentStreakDays, s.LongestStreakDays, s.TotalStudyDays, s.UpdatedOnUtc)).ToArray();
        }
        await using (var restart = Factory())
        {
            _ = restart.CreateClient();
            await using var scope = restart.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StudyDbContext>();
            var streaks = await db.StudyStreaks.OrderBy(s => s.StudentId).ToArrayAsync();
            Assert.Equal(repaired, streaks.Select(s => (s.Id, s.CurrentStreakDays, s.LongestStreakDays, s.TotalStudyDays, s.UpdatedOnUtc)).ToArray());
        }
    }

    private sealed class FailMarkerSave : SaveChangesInterceptor
    {
        public const string Message = "Simulated repair marker save failure";
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ModuleState>().Any(entry => entry.State == EntityState.Added))
                throw new InvalidOperationException(Message);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> previous;
        public TestEnvironment(Dictionary<string, string?> values)
        {
            previous = values.ToDictionary(pair => pair.Key, pair => Environment.GetEnvironmentVariable(pair.Key));
            foreach (var (key, value) in values) Environment.SetEnvironmentVariable(key, value);
        }
        public void Dispose()
        {
            foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static WebApplicationFactory<Program> Factory()
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Staging"));
    private static Task<JsonElement> Manual(HttpClient client, Guid student, DateTime studiedOn, int minutes)
        => Json(client.PostAsJsonAsync("/api/study/sessions/manual", new
        { studentId = student, subject = "Mat", effectiveMinutes = minutes, studiedOnUtc = studiedOn }));
    private static Task<JsonElement> Streak(HttpClient client, Guid student)
        => Json(client.GetAsync($"/api/study/students/{student}/streak"));
    private static async Task<JsonElement> Json(Task<HttpResponseMessage> task) => await Json(await task);
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
            using var json = JsonDocument.Parse(text);
            return json.RootElement.Clone();
        }
    }
}
