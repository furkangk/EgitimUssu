using System.Text.Json;
using EgitimUssu.Modules.Assignments.Domain;
using EgitimUssu.Modules.Assignments.Infrastructure;
using EgitimUssu.Modules.Membership.Domain;
using EgitimUssu.Modules.Membership.Infrastructure;
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
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Middleware;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EgitimUssu.Tests.Unit;

public sealed class ConcurrencyConventionTests
{
    public static TheoryData<Type, Type> CriticalEntities => new()
    {
        { typeof(PaymentsDbContext), typeof(PaymentRecord) },
        { typeof(MembershipDbContext), typeof(UserSubscription) },
        { typeof(StudyDbContext), typeof(StudyStreak) },
        { typeof(StudyDbContext), typeof(StudyTopic) },
        { typeof(ProgressTrackingDbContext), typeof(TopicMastery) },
        { typeof(AssignmentsDbContext), typeof(Assignment) },
        { typeof(SchedulingDbContext), typeof(LessonSchedule) },
        { typeof(StudentsDbContext), typeof(TeacherStudentLink) },
        { typeof(StudentsDbContext), typeof(StudentProfile) },
        { typeof(ParentsDbContext), typeof(ParentChildLink) },
    };

    [Theory]
    [MemberData(nameof(CriticalEntities))]
    public void Critical_Entity_Uses_Database_Generated_Xmin(Type contextType, Type entityType)
    {
        var builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(builderType)!;
        builder.UseNpgsql("Host=localhost;Database=model_only;Username=model_only");
        using var db = (ModuleDbContext)Activator.CreateInstance(contextType, builder.Options, new JsonDomainEventMapper())!;
        var entity = db.Model.FindEntityType(entityType)!;
        var version = entity.FindProperty(nameof(IVersioned.Version));
        Assert.NotNull(version);

        Assert.True(typeof(IVersioned).IsAssignableFrom(entityType));
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
        Assert.Equal("xmin", version.GetColumnName());
        Assert.Equal("xid", version.GetColumnType());
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(OutboxMessage))!.GetProperties(), p => p.IsConcurrencyToken);

        // PostgreSQL sistem kolonu için her iki yönde de DDL üretilmemeli.
        var migrations = db.Database.GetMigrations().ToArray();
        var versionIndex = Array.FindIndex(migrations, name => name.EndsWith("_AddVersionToken", StringComparison.Ordinal));
        Assert.True(versionIndex > 0, "AddVersionToken ve önceki migration bulunmalı.");
        var previousMigration = migrations[versionIndex - 1];
        var versionMigration = migrations[versionIndex];
        var migrator = db.GetService<IMigrator>();
        foreach (var sql in new[]
        {
            migrator.GenerateScript(previousMigration, versionMigration),
            migrator.GenerateScript(versionMigration, previousMigration)
        })
        {
            Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(true, 409, "shared.concurrency_conflict", "Kayıt siz düzenlerken değişti. Lütfen yenileyip tekrar deneyin.")]
    [InlineData(false, 500, "shared.unexpected", "The request could not be completed.")]
    public async Task Middleware_Maps_Concurrency_Separately_From_Unexpected_Errors(
        bool concurrency, int status, string code, string message)
    {
        using var provider = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();
        var middleware = new ProblemDetailsExceptionMiddleware(
            _ => throw (concurrency ? new DbUpdateConcurrencyException("private database detail") : new InvalidOperationException("private database detail")),
            NullLogger<ProblemDetailsExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(message, json.RootElement.GetProperty("message").GetString());
        Assert.Equal(status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(context.TraceIdentifier, json.RootElement.GetProperty("traceId").GetString());
    }
}
