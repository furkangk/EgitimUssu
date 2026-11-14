using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Messaging.Domain;
using EgitimUssu.Modules.Messaging.Infrastructure;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 5 / N-46, K-28: Mesajlaşmada süregelen ilişki kontrolü — gerçek Postgres + gerçek modül zinciri.
/// <list type="bullet">
/// <item>İlişki <b>yalnız sohbet açılırken</b> soruluyordu: bağ arşivlenince ya da veli bağı iptal edilince taraflar
/// aynı sohbete yazmayı sürdürüyordu. Artık gönderim <c>403 messaging.relationship_ended</c>, geçmiş okunur,
/// sohbet <c>isReadOnly: true</c>.</item>
/// <item>#189: Messaging birleşmeyi tüketmiyordu. Birleşen (manuel) profildeki sohbet, ilişki kanonik profile
/// taşındığı için salt-okunur sayılırdı — artık sohbet de kanonik kimliğe taşınır; aynı üçlü kanonikte varsa
/// mesajlar kanonik sohbete katılır.</item>
/// </list>
/// HTTP düzeyinde koşar çünkü yeni hata kodunun 403 eşlemesi yalnız gerçek host'ta görünür (#182).
/// </summary>
[Collection("containers")]
public sealed class RealMessagingRelationshipTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Archived_Link_Closes_Sending_Keeps_History_And_Unarchive_Reopens()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.155.0.1");
        var student = NewClient(factory, "10.155.0.2");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Iliski-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, "Iliski-Ogrenci");
        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);

        var conversation = await OpenAsync(teacher, Guid.Empty, "Student", studentId);
        var conversationId = conversation.GetProperty("id").GetGuid();
        Assert.False(conversation.GetProperty("isReadOnly").GetBoolean());

        // Pozitif kontrol (#196): ilişki sürerken iki taraf da yazar.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(teacher, conversationId, "Merhaba")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(student, conversationId, "Merhaba hocam")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync(
            $"/api/students/teachers/{teacherUserId}/students/{studentId}/archive", content: null)).StatusCode);

        // Gönderim iki taraf için de kapanır — kod korunur (shared.forbidden'a çevrilmez).
        var fromTeacher = await SendAsync(teacher, conversationId, "Hâlâ orada mısın?");
        Assert.Equal(HttpStatusCode.Forbidden, fromTeacher.StatusCode);
        Assert.Equal("messaging.relationship_ended", await ErrorCodeAsync(fromTeacher));
        var fromStudent = await SendAsync(student, conversationId, "Hocam?");
        Assert.Equal(HttpStatusCode.Forbidden, fromStudent.StatusCode);
        Assert.Equal("messaging.relationship_ended", await ErrorCodeAsync(fromStudent));

        // Geçmiş okunur, gelen kutusu ve yeniden açma salt-okunur gösterir.
        Assert.Equal(2, (await ReadListAsync(student, $"/api/messaging/conversations/{conversationId}/messages")).Count);
        Assert.True(InboxRow(await ReadListAsync(teacher, "/api/messaging/conversations"), conversationId).GetProperty("isReadOnly").GetBoolean());
        Assert.True(InboxRow(await ReadListAsync(student, "/api/messaging/conversations"), conversationId).GetProperty("isReadOnly").GetBoolean());
        var reopened = await OpenAsync(teacher, Guid.Empty, "Student", studentId);
        Assert.Equal(conversationId, reopened.GetProperty("id").GetGuid());
        Assert.True(reopened.GetProperty("isReadOnly").GetBoolean());

        // Bilgi saklanmaz: bağ arşivden çıkınca gönderim kendiliğinden geri açılır.
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync(
            $"/api/students/teachers/{teacherUserId}/students/{studentId}/unarchive", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(teacher, conversationId, "Yeniden merhaba")).StatusCode);
        Assert.False(InboxRow(await ReadListAsync(student, "/api/messaging/conversations"), conversationId).GetProperty("isReadOnly").GetBoolean());
    }

    [SkippableFact]
    public async Task Revoked_Parent_Cannot_Send_At_Once()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.155.1.1");
        var parent = NewClient(factory, "10.155.1.2");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Veli-Mesaj-Ogretmen");
        var parentUserId = await RegisterAsync(parent, role: 4, "Veli-Mesaj-Veli");
        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        var linkId = await ClaimParentInviteAsync(parent, await CreateParentInviteAsync(teacher, studentId, teacherUserId));
        await DrainOutboxAsync(factory);

        var conversationId = (await OpenAsync(teacher, parentUserId, "Parent", studentId)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(parent, conversationId, "Merhaba hocam")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await parent.PostAsync($"/api/parents/children/{linkId}/revoke", null)).StatusCode);

        // Outbox boşaltılmadan: kontrol türetilmiş alanı değil, Parents'taki onaylı bağı okur (#191).
        var fromParent = await SendAsync(parent, conversationId, "Bir sorum var");
        Assert.Equal(HttpStatusCode.Forbidden, fromParent.StatusCode);
        Assert.Equal("messaging.relationship_ended", await ErrorCodeAsync(fromParent));
        var fromTeacher = await SendAsync(teacher, conversationId, "Buyurun");
        Assert.Equal(HttpStatusCode.Forbidden, fromTeacher.StatusCode);
        Assert.True(InboxRow(await ReadListAsync(parent, "/api/messaging/conversations"), conversationId).GetProperty("isReadOnly").GetBoolean());
        Assert.Single(await ReadListAsync(parent, $"/api/messaging/conversations/{conversationId}/messages"));
    }

    /// <summary>
    /// #189: Öğretmenin açtığı profildeki veli sohbeti, öğrenci kendi profiliyle davet kodunu girince (birleşme)
    /// kanonik profile taşınır ve yazılabilir kalır. Sohbet kimliği değişmez — bildirim rotaları geçerli kalır.
    /// </summary>
    [SkippableFact]
    public async Task Conversation_On_A_Merged_Profile_Moves_To_The_Canonical_Profile_And_Stays_Writable()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.155.2.1");
        var student = NewClient(factory, "10.155.2.2");
        var parent = NewClient(factory, "10.155.2.3");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Birlesik-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, "Birlesik-Ogrenci");
        var parentUserId = await RegisterAsync(parent, role: 4, "Birlesik-Veli");
        var canonicalId = await CreateOwnProfileAsync(student, studentUserId);
        var manualId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        await ClaimParentInviteAsync(parent, await CreateParentInviteAsync(teacher, manualId, teacherUserId));
        await DrainOutboxAsync(factory);

        var conversationId = (await OpenAsync(teacher, parentUserId, "Parent", manualId)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(parent, conversationId, "Merhaba hocam")).StatusCode);

        await ClaimStudentInviteAsync(factory, teacher, student, teacherUserId, manualId, studentUserId);
        await DrainOutboxAsync(factory);
        Assert.Equal(canonicalId, await ReadMergedIntoAsync(factory, manualId));

        var row = InboxRow(await ReadListAsync(parent, "/api/messaging/conversations"), conversationId);
        Assert.Equal(canonicalId, row.GetProperty("studentId").GetGuid());
        Assert.False(row.GetProperty("isReadOnly").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(parent, conversationId, "Birleşmeden sonra")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(teacher, conversationId, "Görüyorum")).StatusCode);
        Assert.Equal(3, (await ReadListAsync(teacher, $"/api/messaging/conversations/{conversationId}/messages")).Count);
    }

    /// <summary>
    /// #189 çakışma yolu (#187): kanonik profilde aynı (öğretmen, veli) sohbeti zaten varsa benzersiz üçlü indeksi
    /// kör taşımayı reddeder ve mesaj outbox'ta sonsuza dek yeniden denenir. Kanonik kazanır: kaynak sohbetin mesajları
    /// kanoniğe katılır, daha yeni son mesaj alınır, engel kaybolmaz, kaynak sohbet silinir.
    /// </summary>
    [SkippableFact]
    public async Task Merge_Into_An_Existing_Conversation_Moves_Messages_And_Keeps_The_Block()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.155.3.1");
        var student = NewClient(factory, "10.155.3.2");
        var parent = NewClient(factory, "10.155.3.3");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Cakisma-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, "Cakisma-Ogrenci");
        var parentUserId = await RegisterAsync(parent, role: 4, "Cakisma-Veli");
        var canonicalId = await CreateOwnProfileAsync(student, studentUserId);
        var manualId = await CreateTeacherProfileAsync(teacher, teacherUserId);
        await ClaimParentInviteAsync(parent, await CreateParentInviteAsync(teacher, manualId, teacherUserId));
        await DrainOutboxAsync(factory);

        // Kanonik profilde aynı üçlüye ait, daha eski bir sohbet (ör. öğretmen ve veli önceden o profil üzerinden yazışmış).
        var canonicalConversationId = Guid.NewGuid();
        var olderUtc = DateTime.UtcNow.AddMinutes(-30);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var messaging = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            var existing = Conversation.Create(
                canonicalConversationId, ParticipantRole.Teacher, teacherUserId, ParticipantRole.Parent, parentUserId, canonicalId, olderUtc).Value!;
            var oldMessage = existing.SendMessage(Guid.NewGuid(), teacherUserId, "Eski kanonik mesaj", olderUtc).Value!;
            existing.ClearDomainEvents();
            messaging.Conversations.Add(existing);
            messaging.Messages.Add(oldMessage);
            await messaging.SaveChangesAsync();
        }

        var sourceConversationId = (await OpenAsync(teacher, parentUserId, "Parent", manualId)).GetProperty("id").GetGuid();
        Assert.NotEqual(canonicalConversationId, sourceConversationId);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(parent, sourceConversationId, "Yeni mesaj")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await parent.PostAsync($"/api/messaging/conversations/{sourceConversationId}/block", null)).StatusCode);

        await ClaimStudentInviteAsync(factory, teacher, student, teacherUserId, manualId, studentUserId);
        await DrainOutboxAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var messaging = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            Assert.False(await messaging.Conversations.AnyAsync(x => x.Id == sourceConversationId));
            Assert.False(await messaging.Messages.AnyAsync(x => x.ConversationId == sourceConversationId));
            var canonical = await messaging.Conversations.AsNoTracking().SingleAsync(x => x.Id == canonicalConversationId);
            Assert.Equal("Yeni mesaj", canonical.LastMessagePreview);
            Assert.True(canonical.IsBlockedByCounterpart);
        }

        Assert.Equal(2, (await ReadListAsync(teacher, $"/api/messaging/conversations/{canonicalConversationId}/messages")).Count);
        var row = InboxRow(await ReadListAsync(parent, "/api/messaging/conversations"), canonicalConversationId);
        Assert.True(row.GetProperty("isBlocked").GetBoolean());
        Assert.False(row.GetProperty("isReadOnly").GetBoolean());

        // Engeli koyan veli kaldırınca ilişki kanonikte sürdüğü için yazışma devam eder.
        Assert.Equal(HttpStatusCode.OK, (await parent.PostAsync($"/api/messaging/conversations/{canonicalConversationId}/unblock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(parent, canonicalConversationId, "Devam edelim")).StatusCode);
    }

    private static JsonElement InboxRow(List<JsonElement> inbox, Guid conversationId)
        => inbox.Single(row => row.GetProperty("id").GetGuid() == conversationId);

    private static async Task<JsonElement> OpenAsync(HttpClient client, Guid counterpartUserId, string counterpartRole, Guid studentId)
    {
        var response = await client.PostAsJsonAsync("/api/messaging/conversations", new { counterpartUserId, counterpartRole, studentId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Guid conversationId, string body)
        => client.PostAsJsonAsync($"/api/messaging/conversations/{conversationId}/messages", new { body });

    /// <summary>Öğretmen öğrenciyi davet eder, öğrenci kodu girer; öğrencinin kendi profili varsa bu bir birleşmedir.</summary>
    private static async Task ClaimStudentInviteAsync(
        WebApplicationFactory<Program> factory, HttpClient teacher, HttpClient student, Guid teacherUserId, Guid studentId, Guid studentUserId)
        => await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);

    private static async Task<Guid?> ReadMergedIntoAsync(WebApplicationFactory<Program> factory, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<StudentsDbContext>().StudentProfiles.AsNoTracking()
            .SingleAsync(x => x.Id == studentId)).MergedIntoStudentId;
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
            email = $"mesaj-iliski-{Guid.NewGuid():N}@test.com",
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

    private static async Task<List<JsonElement>> ReadListAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray().Select(row => row.Clone()).ToList();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
