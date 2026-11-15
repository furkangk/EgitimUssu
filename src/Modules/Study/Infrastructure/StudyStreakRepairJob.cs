using EgitimUssu.Modules.Study.Application;
using EgitimUssu.Modules.Study.Domain;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Study.Infrastructure;

/// <summary>N-40: Seansları kaynak kabul eden, kalıcı işaretle tek seferlik seri onarımı.</summary>
public sealed class StudyStreakRepairJob(StudyDbContext db, ILocalCalendar calendar, IClock clock, IIdGenerator ids)
{
    private static readonly Guid Marker = Guid.Parse("a1900001-0000-4000-8000-000000000001");

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var repaired = 0;
        var attempt = 0;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            repaired = 0;
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            // Süreli kira yerine transaction kilidi: uzun onarımda da ikinci örnek giremez.
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(hashtext('study:streak-repair-v1'))", cancellationToken);
            if (await db.ModuleStates.AnyAsync(s => s.Id == Marker, cancellationToken)) return;

            var students = await db.StudyStudents.Select(s => s.Id)
                .Union(db.StudySessions.Select(s => s.StudentId))
                .Union(db.StudyStreaks.Select(s => s.StudentId))
                .Distinct().ToArrayAsync(cancellationToken);
            var now = clock.UtcNow;
            var today = calendar.StudyDay(now);
            var (_, end) = calendar.StudyDayRangeUtc(today);
            foreach (var batch in students.Chunk(100))
            {
                var sessions = await db.StudySessions.Where(s => batch.Contains(s.StudentId)
                    && s.Status == StudySessionStatus.Completed && s.EndedAtUtc != null && s.EndedAtUtc < end)
                    .AsNoTracking().ToArrayAsync(cancellationToken);
                var goals = await db.StudyGoals.Where(g => batch.Contains(g.StudentId) && g.IsActive)
                    .AsNoTracking().ToArrayAsync(cancellationToken);
                var streaks = await db.StudyStreaks.Where(s => batch.Contains(s.StudentId)).ToListAsync(cancellationToken);
                foreach (var student in batch)
                {
                    var streak = streaks.SingleOrDefault(s => s.StudentId == student);
                    if (streak is null)
                    {
                        streak = new StudyStreak(ids.New(), student, now);
                        db.StudyStreaks.Add(streak);
                    }
                    var goal = goals.Where(g => g.StudentId == student).OrderByDescending(g => g.UpdatedOnUtc).FirstOrDefault();
                    streak.Recalculate(StudyStreakCalculator.QualifyingDays(
                        sessions.Where(s => s.StudentId == student), null, goal, calendar), today, now);
                    repaired++;
                }
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }
            db.ModuleStates.Add(new ModuleState { Id = Marker, CreatedOnUtc = now });
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        });
        return repaired;
    }
}

/// <summary>Migration/seed sonrası, HTTP öncesi; bayrak varsayılan kapalı, başarısızlık açılışı durdurur.</summary>
internal sealed class StudyStreakRepairInitializer(
    IServiceScopeFactory scopes, IConfiguration configuration, ILogger<StudyStreakRepairInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Study:RepairStreaksOnStartup")) return;
        await using var scope = scopes.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<StudyStreakRepairJob>().RunAsync(cancellationToken);
        logger.LogInformation("Study streak repair initialized: {StudentCount} student(s).", count);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
