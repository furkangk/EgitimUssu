using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Parents.Application;
using EgitimUssu.Modules.Parents.Domain;
using EgitimUssu.Modules.Parents.Infrastructure;
using EgitimUssu.Modules.Parents.Infrastructure.Migrations;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 4 / N-26, N-27, N-32: Veli erişiminin iptali, gizliliğin fail-closed okunması ve öğrenci başına tek
/// onaylı birincil veli — gerçek Postgres + outbox zinciriyle.
/// <list type="bullet">
/// <item>N-26: bağı iptal edilen veli öğrencinin profilini okumaya devam ediyordu (erişim yalnız onayda yazılan
/// <c>ParentUserId</c>'den okunuyordu); reddedilmiş bağ yeniden onaylanabiliyordu.</item>
/// <item>N-27: davetle bağlanan öğrencinin kullanıcısı Parents'a hiç ulaşmıyordu — öğrenci veli talebini onaylayamıyor,
/// "Gizli" seçse de veli paneli çalışma verisini gösteriyordu.</item>
/// <item>N-32: birincil veli tekilliği yalnız uygulamada kontrol ediliyordu; eşzamanlı iki onay iki birincil üretirdi.</item>
/// </list>
/// </summary>
[Collection("containers")]
public sealed class RealParentAccessRevocationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Revoked_Parent_Loses_Profile_Access_At_Once_And_The_Profile_Forgets_The_Parent()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.154.0.1");
        var parent = NewClient(factory, "10.154.0.2");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Iptal-Ogretmen");
        var parentUserId = await RegisterAsync(parent, role: 4, "Iptal-Veli");
        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        var linkId = await ClaimParentInviteAsync(parent, await CreateParentInviteAsync(teacher, studentId, teacherUserId));

        await DrainOutboxAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await parent.GetAsync($"/api/students/profiles/{studentId}")).StatusCode);
        Assert.Equal(parentUserId, await ReadProfileParentAsync(factory, studentId));

        Assert.Equal(HttpStatusCode.OK, (await parent.PostAsync($"/api/parents/children/{linkId}/revoke", null)).StatusCode);

        // Olay henüz işlenmeden bile erişim kesilir: yetki onaylı bağdan okunur.
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync($"/api/students/profiles/{studentId}")).StatusCode);

        // Bitiş olayı Students'ın tüketicisine ulaşır (gerçek DI kaydı — #157): profil veliyi unutur.
        await DrainOutboxAsync(factory);
        Assert.Null(await ReadProfileParentAsync(factory, studentId));
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync($"/api/students/profiles/{studentId}")).StatusCode);
    }

    [SkippableFact]
    public async Task Rejected_Request_Cannot_Be_Approved_Later()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var student = NewClient(factory, "10.154.1.1");
        var parent = NewClient(factory, "10.154.1.2");
        var studentUserId = await RegisterAsync(student, role: 3, "Ret-Ogrenci");
        var parentUserId = await RegisterAsync(parent, role: 4, "Ret-Veli");
        var studentId = await CreateOwnProfileAsync(student, studentUserId);
        await DrainOutboxAsync(factory);

        var linkId = await RequestLinkAsync(parent, parentUserId, studentId);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/parents/children/{linkId}/reject", null)).StatusCode);

        var approve = await student.PostAsync($"/api/parents/children/{linkId}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        Assert.Equal("parents.invalid_link_transition", (await ReadJsonAsync(approve)).GetProperty("code").GetString());
    }

    [SkippableFact]
    public async Task Student_Linked_By_Invite_Can_Approve_A_Parent_Request()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.154.2.1");
        var student = NewClient(factory, "10.154.2.2");
        var parent = NewClient(factory, "10.154.2.3");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Onay-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, "Onay-Ogrenci");
        var parentUserId = await RegisterAsync(parent, role: 4, "Onay-Veli");
        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);
        await DrainOutboxAsync(factory);

        var linkId = await RequestLinkAsync(parent, parentUserId, studentId);
        var approve = await student.PostAsync($"/api/parents/children/{linkId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Equal("Approved", (await ReadJsonAsync(approve)).GetProperty("status").GetString());
    }

    /// <summary>
    /// Yetki testi kuralı (plan Global Constraints): N-27 onay yetkisini davetle bağlanan öğrenciye genişletti — genişleme
    /// yalnız <b>kendi</b> profiline olmalı. Başka bir profile davetle bağlanmış öğrenci, ilgisiz veli ve talebi açan velinin
    /// kendisi başkasının bağını onaylayamaz/reddedemez; iptal de yalnız bağın velisi, öğrencisi ya da Admin'indir.
    /// </summary>
    [SkippableFact]
    public async Task Only_The_Linked_Student_Decides_On_A_Parent_Request()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.154.5.1");
        var student = NewClient(factory, "10.154.5.2");
        var otherStudent = NewClient(factory, "10.154.5.3");
        var parent = NewClient(factory, "10.154.5.4");
        var unrelatedParent = NewClient(factory, "10.154.5.5");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Yetki-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, "Yetki-Ogrenci");
        var otherStudentUserId = await RegisterAsync(otherStudent, role: 3, "Yetki-Baska-Ogrenci");
        var parentUserId = await RegisterAsync(parent, role: 4, "Yetki-Veli");
        await RegisterAsync(unrelatedParent, role: 4, "Yetki-Ilgisiz-Veli");
        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        var otherStudentId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, otherStudent, teacherUserId, otherStudentId, otherStudentUserId);
        await DrainOutboxAsync(factory);

        var linkId = await RequestLinkAsync(parent, parentUserId, studentId);

        foreach (var outsider in new[] { otherStudent, unrelatedParent })
        {
            foreach (var action in new[] { "approve", "reject", "revoke" })
            {
                var response = await outsider.PostAsync($"/api/parents/children/{linkId}/{action}", null);
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{action}: {(int)response.StatusCode}");
            }
        }

        // Veli kendi talebini onaylayamaz/reddedemez (iptal edebilir — kendi bağı).
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostAsync($"/api/parents/children/{linkId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PostAsync($"/api/parents/children/{linkId}/reject", null)).StatusCode);

        // Bağ hâlâ bekliyor; karar profilin öğrencisinde.
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsync($"/api/parents/children/{linkId}/approve", null)).StatusCode);
    }

    [SkippableFact]
    public async Task Hidden_Privacy_Of_A_Student_Linked_By_Invite_Is_Respected_On_The_Parent_Dashboard()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.154.3.1");
        var student = NewClient(factory, "10.154.3.2");
        var parent = NewClient(factory, "10.154.3.3");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Gizli-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, "Gizli-Ogrenci");
        var parentUserId = await RegisterAsync(parent, role: 4, "Gizli-Veli");
        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);
        await ClaimParentInviteAsync(parent, await CreateParentInviteAsync(teacher, studentId, teacherUserId));
        await DrainOutboxAsync(factory);

        // Pozitif kontrol: öğrenci henüz kısıtlamadı → paylaşım açık. Bu, eşlemenin gerçekten geldiğini kanıtlar;
        // yoksa aşağıdaki "gizli" sonucu tüketiciden değil fail-closed varsayılandan da gelebilirdi.
        var before = await parent.GetAsync($"/api/parents/{parentUserId}/children/{studentId}/dashboard");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.True((await ReadJsonAsync(before)).GetProperty("study").GetProperty("isShared").GetBoolean());

        var hidden = await student.PutAsJsonAsync($"/api/settings/users/{studentUserId}", new
        {
            pushNotificationsEnabled = true,
            emailNotificationsEnabled = true,
            upcomingLessonReminderEnabled = true,
            homeworkReminderEnabled = true,
            paymentReminderEnabled = true,
            weeklySummaryEnabled = true,
            privacyLevel = 3,             // PrivacyLevel.Hidden
            sessionTerminationPolicy = 1, // KeepLatest
        });
        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        await DrainOutboxAsync(factory);

        var dashboard = await parent.GetAsync($"/api/parents/{parentUserId}/children/{studentId}/dashboard");

        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.False((await ReadJsonAsync(dashboard)).GetProperty("study").GetProperty("isShared").GetBoolean());
    }

    /// <summary>
    /// Deterministik yarış: iki bekleyen birincil bağ, ikisi de "henüz birincil yok" görerek onaylanır. Uygulama
    /// kontrolü ikisini de geçirir; kısmi benzersiz indeks ikinciyi reddeder ve depo bunu <c>parents.primary_exists</c>e çevirir.
    /// </summary>
    [SkippableFact]
    public async Task Second_Primary_Approved_In_A_Race_Is_Rejected_By_The_Database()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var studentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var first = new ParentChildLink(Guid.NewGuid(), Guid.NewGuid(), studentId, "Ada", "Anne", null, true, now);
        var second = new ParentChildLink(Guid.NewGuid(), Guid.NewGuid(), studentId, "Ada", "Baba", null, true, now);
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var parents = seed.ServiceProvider.GetRequiredService<ParentsDbContext>();
            parents.ParentChildLinks.AddRange(first, second);
            await parents.SaveChangesAsync();
        }

        await using var late = factory.Services.CreateAsyncScope();
        var lateRepository = late.ServiceProvider.GetRequiredService<IParentRepository>();
        var lateLink = (await lateRepository.GetLinkByIdAsync(second.Id, CancellationToken.None))!;
        Assert.Empty(await lateRepository.ListApprovedLinksForStudentAsync(studentId, CancellationToken.None));

        await using (var early = factory.Services.CreateAsyncScope())
        {
            var earlyRepository = early.ServiceProvider.GetRequiredService<IParentRepository>();
            var earlyLink = (await earlyRepository.GetLinkByIdAsync(first.Id, CancellationToken.None))!;
            Assert.True(earlyLink.Approve(Guid.NewGuid(), null, now).IsSuccess);
            Assert.True((await earlyRepository.SaveLinkChangesAsync(CancellationToken.None)).IsSuccess);
        }

        Assert.True(lateLink.Approve(Guid.NewGuid(), null, now).IsSuccess);
        var saved = await lateRepository.SaveLinkChangesAsync(CancellationToken.None);

        Assert.True(saved.IsFailure);
        Assert.Equal("parents.primary_exists", saved.Error.Code);
        Assert.Equal(1, await CountApprovedPrimariesAsync(factory, studentId));
    }

    /// <summary>Gerçek HTTP yarışı: sonuç sırası belirsiz, ama değişmez kural her koşuda geçerli — 5xx yok, tek birincil.</summary>
    [SkippableFact]
    public async Task Concurrent_Parent_Claims_Never_Fail_With_500_And_Leave_One_Primary()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.154.4.1");
        var mother = NewClient(factory, "10.154.4.2");
        var father = NewClient(factory, "10.154.4.3");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Yaris-Ogretmen");
        await RegisterAsync(mother, role: 4, "Yaris-Anne");
        await RegisterAsync(father, role: 4, "Yaris-Baba");

        for (var round = 0; round < 3; round++)
        {
            var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId);
            var motherCode = await CreateParentInviteAsync(teacher, studentId, teacherUserId);
            var fatherCode = await CreateParentInviteAsync(teacher, studentId, teacherUserId);

            var responses = await Task.WhenAll(
                mother.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode = motherCode }),
                father.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode = fatherCode }));

            Assert.All(responses, response => Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                $"Beklenmeyen durum: {(int)response.StatusCode}"));
            Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Equal(1, await CountApprovedPrimariesAsync(factory, studentId));
        }
    }

    /// <summary>
    /// Migration'ın veri göçü (master §1 "Veri göçü"): indeks kurulmadan önce mevcut fazla birincil veliler en eskisi
    /// hariç ikincile çevrilir. Postgres'te DDL transaction'lıdır — indeks geçici olarak kaldırılıp tekrarlar yazılır,
    /// göç SQL'i koşulur, sonuç okunur ve her şey geri alınır.
    /// </summary>
    [SkippableFact]
    public async Task Migration_Demotes_Duplicate_Primaries_Keeping_The_Earliest()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var studentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var earliest = new ParentChildLink(Guid.NewGuid(), Guid.NewGuid(), studentId, "Ada", "Anne", null, true, now.AddDays(-3));
        Assert.True(earliest.Approve(Guid.NewGuid(), null, now.AddDays(-3)).IsSuccess);
        var later = new ParentChildLink(Guid.NewGuid(), Guid.NewGuid(), studentId, "Ada", "Baba", null, true, now.AddDays(-1));
        Assert.True(later.Approve(Guid.NewGuid(), null, now.AddDays(-1)).IsSuccess);

        await using var scope = factory.Services.CreateAsyncScope();
        var parents = scope.ServiceProvider.GetRequiredService<ParentsDbContext>();

        // Yeniden denemeli strateji kullanıcı transaction'ını yalnız kendi ExecuteAsync'i içinde kabul eder.
        var primaries = await parents.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await parents.Database.BeginTransactionAsync();
            await parents.Database.ExecuteSqlRawAsync($"DROP INDEX parents.\"{ParentLinkIntegrity.PrimaryContactIndexName}\"");
            parents.ParentChildLinks.AddRange(earliest, later);
            await parents.SaveChangesAsync();

            await parents.Database.ExecuteSqlRawAsync(ParentLinkIntegrity.DemoteDuplicatePrimariesSql);

            var remaining = await parents.ParentChildLinks.AsNoTracking()
                .Where(x => x.StudentId == studentId && x.IsPrimaryContact)
                .Select(x => x.Id)
                .ToListAsync();
            await transaction.RollbackAsync();
            return remaining;
        });

        Assert.Equal(earliest.Id, Assert.Single(primaries));

        // Geri alma hem satırları hem indeksi geri getirdi (paylaşılan veritabanı sonraki testlere sağlam kalır).
        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ParentsDbContext>();
        Assert.False(await verifyDb.ParentChildLinks.AnyAsync(x => x.StudentId == studentId));
        Assert.Equal(1, await verifyDb.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_indexes WHERE schemaname = 'parents' AND indexname = {ParentLinkIntegrity.PrimaryContactIndexName}")
            .SingleAsync());
    }

    private static async Task<int> CountApprovedPrimariesAsync(WebApplicationFactory<Program> factory, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ParentsDbContext>().ParentChildLinks
            .CountAsync(x => x.StudentId == studentId && x.Status == ParentChildLinkStatus.Approved && x.IsPrimaryContact);
    }

    private static async Task<Guid?> ReadProfileParentAsync(WebApplicationFactory<Program> factory, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<StudentsDbContext>().StudentProfiles.AsNoTracking()
            .SingleAsync(x => x.Id == studentId)).ParentUserId;
    }

    private static async Task<Guid> RequestLinkAsync(HttpClient parent, Guid parentUserId, Guid studentId)
    {
        var response = await parent.PostAsJsonAsync("/api/parents/children/link", new
        {
            parentUserId,
            studentId,
            relationship = "Anne",
            childDisplayName = "Ada",
            inviteCode = (string?)null,
            isPrimaryContact = true,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> ClaimParentInviteAsync(HttpClient parent, string code)
    {
        var response = await parent.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode = code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<string> CreateParentInviteAsync(HttpClient teacher, Guid studentId, Guid teacherUserId)
    {
        var response = await teacher.PostAsJsonAsync(
            $"/api/students/profiles/{studentId}/parent-invite",
            new { teacherUserId, childDisplayName = "Ada" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("inviteCode").GetString()!;
    }

    private static Task<Guid> CreateTeacherProfileAsync(HttpClient teacher, Guid teacherUserId)
        => CreateProfileAsync(teacher, userId: null, createdByTeacherUserId: teacherUserId, origin: 1);

    private static Task<Guid> CreateOwnProfileAsync(HttpClient student, Guid studentUserId)
        => CreateProfileAsync(student, userId: studentUserId, createdByTeacherUserId: null, origin: 2);

    private static async Task<Guid> CreateProfileAsync(HttpClient client, Guid? userId, Guid? createdByTeacherUserId, int origin)
    {
        var response = await client.PostAsJsonAsync("/api/students/profiles", new
        {
            userId,
            createdByTeacherUserId,
            parentUserId = (Guid?)null,
            fullName = "Ada Yilmaz",
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin,
            subjects = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> factory, string forwardedFor)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return client;
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role, string label)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"veli-erisim-{Guid.NewGuid():N}@test.com",
            password = "Passw0rd!",
            firstName = label,
            lastName = "Test",
            phoneNumber = (string?)null,
            roles = new[] { role },
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
        return payload.RootElement.GetProperty("userId").GetGuid();
    }

    /// <summary>Aynı Postgres'i paylaşan testlerin satırları partiyi doldurabilir; kuyruk boşalana kadar tur çevrilir.</summary>
    private static async Task DrainOutboxAsync(WebApplicationFactory<Program> factory)
    {
        for (var round = 0; round < 30; round++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync() == 0)
            {
                return;
            }
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
