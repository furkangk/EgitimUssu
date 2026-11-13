using System.Collections.Concurrent;
using EgitimUssu.Modules.Scheduling.Application;
using EgitimUssu.Modules.Scheduling.Domain;
using EgitimUssu.Modules.Scheduling.Infrastructure;
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P07 Task 3 / O2: Korumalı bir istekte varlık <b>bir kez</b> yükleniyor mu?
/// Kanıt InMemory'den alınamaz — gerçek Postgres'te EF'in ürettiği <c>SELECT</c> komutları sayılır.
/// İki mekanizma doğrulanır: (a) <see cref="IRequestEntityCache"/> (authorizer → handler),
/// (b) repository'nin birincil anahtar aramasında EF kimlik haritası (aynı istekte ikinci sorgu yok).
/// </summary>
[Collection("containers")]
public sealed class RealSingleEntityLoadTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Authorizer_And_Handler_Should_Select_Student_Profile_Once()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        var sql = new SqlCommandLog();
        await using var factory = NewFactory(sql);

        var teacherUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await SeedStudentAsync(factory, studentId, teacherUserId);

        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStudentProfileRepository>();
        var cache = scope.ServiceProvider.GetRequiredService<IRequestEntityCache>();
        var query = new GetStudentProfileByIdQuery(studentId);

        sql.Reset();
        // Profili açan öğretmen ucuz kontrolle geçer — kapsam sorgusu (P15 T3) bu yolda hiç çalışmaz.
        var access = scope.ServiceProvider.GetRequiredService<ITeacherStudentAccess>();
        var authorization = await new StudentProfileQueryAuthorizer(new StubCurrentUser(teacherUserId, "Teacher"), repository, cache, access)
            .Authorize(query, CancellationToken.None);
        var result = await new GetStudentProfileByIdQueryHandler(repository, cache).Handle(query, CancellationToken.None);

        Assert.True(authorization.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, sql.CountSelects("student_profiles"));
    }

    [SkippableFact]
    public async Task Repository_Should_Select_Lesson_Schedule_Once_Within_A_Request()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        var sql = new SqlCommandLog();
        await using var factory = NewFactory(sql);

        var lessonId = Guid.NewGuid();
        await SeedLessonAsync(factory, lessonId);

        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILessonScheduleRepository>();

        sql.Reset();
        var first = await repository.GetByIdAsync(lessonId, CancellationToken.None);
        var second = await repository.GetByIdAsync(lessonId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, sql.CountSelects("lesson_schedules"));
    }

    private static WebApplicationFactory<Program> NewFactory(SqlCommandLog sql)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(sql);
                logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
            }));

    private static async Task SeedStudentAsync(WebApplicationFactory<Program> factory, Guid studentId, Guid teacherUserId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
        dbContext.StudentProfiles.Add(new StudentProfile(
            studentId, null, teacherUserId, null, "Ali Veli", "9",
            null, null, null, null, StudentOrigin.TeacherManaged, true, DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedLessonAsync(WebApplicationFactory<Program> factory, Guid lessonId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var now = DateTime.UtcNow;
        dbContext.LessonSchedules.Add(new LessonSchedule(
            lessonId, Guid.NewGuid(), Guid.NewGuid(), "Matematik", ScheduledLessonFormat.Online,
            now.AddDays(1), now.AddDays(1).AddHours(1), "Europe/Istanbul", null,
            LessonScheduleStatus.Planned, 30, null, null, null, now));
        await dbContext.SaveChangesAsync();
    }

    /// <summary>EF'in "Executed DbCommand" günlüklerini toplar; tablo adına göre SELECT sayar.</summary>
    private sealed class SqlCommandLog : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _commands = new();

        public ILogger CreateLogger(string categoryName)
            => categoryName == "Microsoft.EntityFrameworkCore.Database.Command"
                ? new CollectingLogger(_commands)
                : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Reset() => _commands.Clear();

        public int CountSelects(string tableName)
            => _commands.Count(command =>
                command.Contains("SELECT", StringComparison.Ordinal)
                && command.Contains(tableName, StringComparison.Ordinal));

        public void Dispose()
        {
        }

        private sealed class CollectingLogger(ConcurrentQueue<string> commands) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => commands.Enqueue(formatter(state, exception));
        }
    }

    private sealed class StubCurrentUser(Guid userId, params string[] roles) : ICurrentUser
    {
        public string? UserId { get; } = userId.ToString();
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; } = roles;
        public bool IsAuthenticated => true;
    }
}
