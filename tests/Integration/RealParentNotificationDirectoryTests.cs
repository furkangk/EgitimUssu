using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Shared.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P06 Task 10: Veli bildirim hedefi sorgusu <b>gerçek Postgres'te</b>.
/// Sorgu, iki varlığı özel bir <c>record</c>'a projekte edip <b>sonra</b> filtreliyordu; EF Core
/// kendi tanımladığımız record'un içini göremediği için <c>InvalidOperationException:
/// The LINQ expression ... could not be translated</c> atıyordu. InMemory sağlayıcısı istemci
/// tarafında değerlendirdiği için hatayı gizliyordu — sonuç: outbox'taki <b>her</b> veli-bildirimli
/// olay (ders tamamlama → otomatik ücret, ödev oluşturma) gerçek DB'de kalıcı olarak başarısızdı.
/// </summary>
[Collection("containers")]
public sealed class RealParentNotificationDirectoryTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task GetApprovedParentsForStudent_Should_Translate_To_Sql()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var parentUserId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ParentsDbContext>();
            dbContext.ParentProfiles.Add(new ParentProfile(
                Guid.NewGuid(), parentUserId, "Veli Test", null, "veli@test.com", now));
            var link = new ParentChildLink(
                Guid.NewGuid(), parentUserId, studentId, "Ali", null, null, true, now);
            link.Approve(parentUserId, null, now);
            dbContext.ParentChildLinks.Add(link);
            await dbContext.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var directory = scope.ServiceProvider.GetRequiredService<IParentNotificationDirectory>();

            var targets = await directory.GetApprovedParentsForStudentAsync(studentId, default);
            var target = Assert.Single(targets);
            Assert.Equal(parentUserId, target.ParentUserId);

            var all = await directory.ListAllApprovedTargetsAsync(default);
            Assert.Contains(all, row => row.StudentId == studentId);
        }
    }
}
