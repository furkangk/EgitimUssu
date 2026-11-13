using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P06 Task 10 / M03-2: Branş ekleme-silme <b>gerçek Postgres'te</b>. InMemory sağlayıcısı
/// çocuk koleksiyona eklenen varlığın <c>Added</c> mı <c>Modified</c> mi izlendiğini
/// ayırt etmediği için bu hata yalnız gerçek DB'de yüzeye çıkar (A-01'in aynısı:
/// <c>DbUpdateConcurrencyException — 1 satır beklendi, 0 etkilendi</c>).
/// </summary>
[Collection("containers")]
public sealed class RealStudentSubjectIntegrationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task AddSubject_Should_Insert_Row_On_Real_Postgres()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var studentId = Guid.NewGuid();
        await SeedProfileAsync(factory, studentId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider
                .GetRequiredService<EgitimUssu.Shared.Application.ICommandHandler<AddStudentSubjectCommand, Result<StudentSubjectResponse>>>();
            var result = await handler.Handle(new AddStudentSubjectCommand(studentId, "Fizik", "TYT"), default);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : string.Empty);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
            var subjects = await dbContext.StudentSubjects
                .Where(subject => subject.StudentProfileId == studentId)
                .ToListAsync();

            Assert.Equal(2, subjects.Count);
            Assert.Contains(subjects, subject => subject.Subject == "Fizik");
        }
    }

    [SkippableFact]
    public async Task RemoveSubject_Should_Delete_Row_On_Real_Postgres()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var studentId = Guid.NewGuid();
        var subjectId = await SeedProfileAsync(factory, studentId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider
                .GetRequiredService<EgitimUssu.Shared.Application.ICommandHandler<RemoveStudentSubjectCommand, Result>>();
            var result = await handler.Handle(new RemoveStudentSubjectCommand(studentId, subjectId), default);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : string.Empty);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
            Assert.Empty(await dbContext.StudentSubjects
                .Where(subject => subject.StudentProfileId == studentId)
                .ToListAsync());
        }
    }

    /// <summary>Bir branşla birlikte profil tohumlar; dönen değer o branşın kimliğidir.</summary>
    private static async Task<Guid> SeedProfileAsync(WebApplicationFactory<Program> factory, Guid studentId)
    {
        var subjectId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
        var profile = new StudentProfile(
            studentId, null, Guid.NewGuid(), null, "Ali Veli", "9",
            null, null, null, null, StudentOrigin.TeacherManaged, true, DateTime.UtcNow);
        profile.Subjects.Add(new StudentSubject(subjectId, studentId, "Matematik", "LGS"));
        dbContext.StudentProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        return subjectId;
    }
}
