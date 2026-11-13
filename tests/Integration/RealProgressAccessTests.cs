using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Students.Domain;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P08 Task 2 / M10-2: Öğretmen gelişim görünümü <b>gerçek Postgres'te</b>.
/// <para>
/// Burada kanıtlanan iki şey birim testiyle görülemez: yeni <c>IsLinkedTeacherOfStudentAsync</c>
/// sorgusunun SQL'e çevrilebilmesi (enum karşılaştırmalı <c>Any</c>) ve uç ile yetkilendiricinin
/// gerçekten bağlı olması — bağ yokken 403, bağ varken 200 (Öğrenilenler #81).
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealProgressAccessTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Teacher_Overview_Should_Be_Forbidden_Without_Link_And_Ok_With_Link()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.31.0.1");

        var teacherUserId = await RegisterTeacherAsync(client);
        var studentId = Guid.NewGuid();

        // Bağ yokken: varsayılan-deny.
        var forbidden = await client.GetAsync(
            $"/api/progress-tracking/teachers/{teacherUserId}/students/{studentId}/overview");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await SeedLinkAsync(factory, teacherUserId, studentId, TeacherStudentLinkStatus.Linked);

        var allowed = await client.GetAsync(
            $"/api/progress-tracking/teachers/{teacherUserId}/students/{studentId}/overview");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        using var payload = JsonDocument.Parse(await allowed.Content.ReadAsStringAsync());
        Assert.Equal(studentId, payload.RootElement.GetProperty("studentId").GetGuid());
        Assert.Equal(0, payload.RootElement.GetProperty("activeGoalCount").GetInt32());
    }

    [SkippableFact]
    public async Task Linked_Teacher_Lookup_Should_Translate_To_Sql()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacherUserId = Guid.NewGuid();
        var linkedStudentId = Guid.NewGuid();
        var archivedStudentId = Guid.NewGuid();
        var rejectedStudentId = Guid.NewGuid();

        await SeedLinkAsync(factory, teacherUserId, linkedStudentId, TeacherStudentLinkStatus.Linked);
        await SeedLinkAsync(factory, teacherUserId, rejectedStudentId, TeacherStudentLinkStatus.Rejected);
        await SeedLinkAsync(factory, teacherUserId, archivedStudentId, TeacherStudentLinkStatus.Linked, archived: true);

        await using var scope = factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IStudentDirectory>();

        Assert.True(await directory.IsLinkedTeacherOfStudentAsync(teacherUserId, linkedStudentId, CancellationToken.None));
        Assert.False(await directory.IsLinkedTeacherOfStudentAsync(teacherUserId, rejectedStudentId, CancellationToken.None));
        Assert.False(await directory.IsLinkedTeacherOfStudentAsync(teacherUserId, archivedStudentId, CancellationToken.None));
        Assert.False(await directory.IsLinkedTeacherOfStudentAsync(Guid.NewGuid(), linkedStudentId, CancellationToken.None));
    }

    private static async Task<Guid> RegisterTeacherAsync(HttpClient client)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"gelisim-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = "Gelişim",
            lastName = "Öğretmen",
            phoneNumber = (string?)null,
            roles = new[] { 2 },   // UserRole.Teacher
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var userId = payload.RootElement.GetProperty("userId").GetGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
        return userId;
    }

    private static async Task SeedLinkAsync(
        WebApplicationFactory<Program> factory,
        Guid teacherUserId,
        Guid studentId,
        TeacherStudentLinkStatus status,
        bool archived = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();
        var link = new TeacherStudentLink(Guid.NewGuid(), teacherUserId, studentId, status, DateTime.UtcNow);
        if (archived)
        {
            link.Archive(DateTime.UtcNow);
        }

        dbContext.TeacherStudentLinks.Add(link);
        await dbContext.SaveChangesAsync();
    }
}
