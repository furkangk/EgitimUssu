using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P10 Task 5 / M16-1 + M09-2: Mesajlaşma zincirinin <b>gerçek Postgres'te</b> uçtan uca doğrulaması —
/// öğretmen sohbeti açar → yazar → outbox → öğrencinin bildirimi → öğrenci okur → öğretmen "okundu" görür.
/// <para>
/// Bu senaryo plan metninde elle koşulacak bir adım olarak yazılmıştı; P06 Task 10'un dersi gereği
/// (InMemory'nin göremediği hatalar ancak gerçek Postgres'te çıkıyor, elle koşum tekrarlanabilir değil)
/// otomatik regresyon testine çevrildi. Burada kanıtlanan şey birim testiyle görülemez: <b>üç ayrı
/// şemanın</b> tek bir akışta doğru bağlanması — Students'taki öğretmen-öğrenci bağı ilişki kapısını
/// besliyor, Messaging'in outbox'ı Notifications'ın inbox'una düşüyor ve okundu damgası geri dönüyor.
/// </para>
/// <para>
/// ⚠️ <b>Push gönderimi bu testin kapsamında değildir</b> (Q8/Firebase hesabı yok): zincir in-app
/// bildirim kaydına kadar doğrulanır. Cihaza gerçek push, `NotificationDispatcher` turunun işidir ve
/// P03'te ayrıca doğrulanmıştır.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealMessagingChainTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Teacher_Message_Should_Reach_Student_Notify_And_Come_Back_As_Read()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", "10.36.0.1");
        var student = factory.CreateClient();
        student.DefaultRequestHeaders.Add("X-Forwarded-For", "10.36.0.2");

        var teacherUserId = await RegisterAsync(teacher, role: 2, label: "Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, label: "Ogrenci");
        var studentId = await CreateStudentAsync(teacher, teacherUserId, studentUserId);

        // 1) Sohbeti öğretmen açar ve karşı tarafı **vermez**: istemci öğrencinin kullanıcı
        //    kimliğini bilmez, sunucu onu studentId'den çözer.
        var opened = await teacher.PostAsJsonAsync("/api/messaging/conversations", new
        {
            counterpartUserId = Guid.Empty,
            counterpartRole = "Student",
            studentId,
        });
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);

        var conversation = await ReadJsonAsync(opened);
        var conversationId = conversation.GetProperty("id").GetGuid();
        Assert.Equal(teacherUserId, conversation.GetProperty("teacherUserId").GetGuid());
        Assert.Equal(studentUserId, conversation.GetProperty("counterpartUserId").GetGuid());
        Assert.False(conversation.GetProperty("isBlocked").GetBoolean());

        // 2) Aynı çağrı ikinci kez yeni satır açmaz (get-or-create).
        var reopened = await teacher.PostAsJsonAsync("/api/messaging/conversations", new
        {
            counterpartUserId = Guid.Empty,
            counterpartRole = "Student",
            studentId,
        });
        Assert.Equal(conversationId, (await ReadJsonAsync(reopened)).GetProperty("id").GetGuid());

        // 3) Öğretmen yazar.
        var sent = await teacher.PostAsJsonAsync(
            $"/api/messaging/conversations/{conversationId}/messages",
            new { body = "Yarınki dersi 15:00'a alalım mı?" });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var message = await ReadJsonAsync(sent);
        Assert.Equal("Yarınki dersi 15:00'a alalım mı?", message.GetProperty("body").GetString());
        Assert.Equal(JsonValueKind.Null, message.GetProperty("readOnUtc").ValueKind);

        // 4) Outbox turu: Messaging'in olayı Notifications'ın tüketicisine düşer.
        await DispatchOutboxUntilNotifiedAsync(factory, studentUserId);

        var notification = await SingleNotificationAsync(factory, studentUserId);
        Assert.Equal(UserNotificationKind.MessageReceived, notification.Kind);
        Assert.Equal($"/messages/{conversationId}", notification.Route);
        Assert.Equal("Yarınki dersi 15:00'a alalım mı?", notification.Body);
        // Push turu ayrı: kayıt bekliyor olarak durur (Q8 olmadan cihaza gitmez).
        Assert.Null(notification.PushedOnUtc);

        // 5) Aynı olayın ikinci turu ikinci bildirim üretmez (inbox idempotency).
        await DispatchOutboxAsync(factory);
        Assert.Equal(1, await CountNotificationsAsync(factory, studentUserId));

        // 6) Öğrencinin gelen kutusunda sohbet ve okunmamış sayacı görünür.
        var inbox = await ReadListAsync(student, "/api/messaging/conversations");
        var row = Assert.Single(inbox);
        Assert.Equal(conversationId, row.GetProperty("id").GetGuid());
        Assert.Equal(1, row.GetProperty("unreadCount").GetInt32());
        // Karşı taraf isteği yapana göredir: öğrenciye öğretmen görünür.
        Assert.Equal(teacherUserId, row.GetProperty("counterpartUserId").GetGuid());
        Assert.Equal("Teacher", row.GetProperty("counterpartRole").GetString());

        // 7) Öğrenci sohbeti açar ve okundu işaretler.
        var studentMessages = await ReadListAsync(
            student, $"/api/messaging/conversations/{conversationId}/messages");
        Assert.Single(studentMessages);

        var read = await student.PostAsync(
            $"/api/messaging/conversations/{conversationId}/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        // 8) Öğretmen tarafında "okundu" görünür — damga geri döner.
        var teacherMessages = await ReadListAsync(
            teacher, $"/api/messaging/conversations/{conversationId}/messages");
        Assert.NotEqual(
            JsonValueKind.Null, teacherMessages.Single().GetProperty("readOnUtc").ValueKind);

        // 9) Okunmamış sayacı sıfırlandı.
        var afterRead = await ReadListAsync(student, "/api/messaging/conversations");
        Assert.Equal(0, afterRead.Single().GetProperty("unreadCount").GetInt32());

        // 10) Öğrenci engeller → öğretmen artık yazamaz; engeli öğretmen kaldıramaz.
        var blocked = await student.PostAsync(
            $"/api/messaging/conversations/{conversationId}/block", content: null);
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);

        var rejected = await teacher.PostAsJsonAsync(
            $"/api/messaging/conversations/{conversationId}/messages", new { body = "Merhaba" });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("messaging.blocked", await ErrorCodeAsync(rejected));

        await teacher.PostAsync($"/api/messaging/conversations/{conversationId}/unblock", content: null);
        var stillRejected = await teacher.PostAsJsonAsync(
            $"/api/messaging/conversations/{conversationId}/messages", new { body = "Merhaba" });
        Assert.Equal(HttpStatusCode.Conflict, stillRejected.StatusCode);

        // 11) Engeli koyan taraf kaldırınca gönderim geri açılır.
        await student.PostAsync($"/api/messaging/conversations/{conversationId}/unblock", content: null);
        var accepted = await teacher.PostAsJsonAsync(
            $"/api/messaging/conversations/{conversationId}/messages", new { body = "Merhaba" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // 12) Yumuşak silme: gönderen siler, satır kalır ama gövde gizlenir.
        var messageId = (await ReadJsonAsync(accepted)).GetProperty("id").GetGuid();
        var deleted = await teacher.DeleteAsync($"/api/messaging/messages/{messageId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await ReadListAsync(
            student, $"/api/messaging/conversations/{conversationId}/messages");
        var deletedRow = afterDelete.Single(row => row.GetProperty("id").GetGuid() == messageId);
        Assert.True(deletedRow.GetProperty("isDeleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, deletedRow.GetProperty("body").ValueKind);
    }

    /// <summary>
    /// İlişki kapısı: bağı olmayan bir öğretmen sohbet açamaz. Bu kural Messaging'in kendi
    /// tablolarında değil, <c>IStudentDirectory</c> üzerinden Students'ın gerçeğinde durur.
    /// </summary>
    [SkippableFact]
    public async Task Unrelated_Teacher_Cannot_Open_Conversation()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var owner = factory.CreateClient();
        owner.DefaultRequestHeaders.Add("X-Forwarded-For", "10.36.0.3");
        var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Add("X-Forwarded-For", "10.36.0.4");
        var studentClient = factory.CreateClient();
        studentClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.36.0.5");

        var ownerUserId = await RegisterAsync(owner, role: 2, label: "Sahip");
        await RegisterAsync(stranger, role: 2, label: "Yabanci");
        var studentUserId = await RegisterAsync(studentClient, role: 3, label: "Ogrenci");
        var studentId = await CreateStudentAsync(owner, ownerUserId, studentUserId);

        var response = await stranger.PostAsJsonAsync("/api/messaging/conversations", new
        {
            counterpartUserId = studentUserId,
            counterpartRole = "Student",
            studentId,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("messaging.relationship_required", await ErrorCodeAsync(response));
    }

    /// <summary>
    /// Hız sınırı gerçek Redis'e karşı: dakikada 30 mesaj, 31.'si 429. Partition <b>kullanıcı</b>
    /// kimliğidir — bu yüzden aynı IP'den konuşan ikinci bir kullanıcı kotadan etkilenmez.
    /// </summary>
    [SkippableFact]
    public async Task Thirty_First_Message_In_A_Minute_Should_Be_Rate_Limited()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", "10.36.0.6");
        var studentClient = factory.CreateClient();
        studentClient.DefaultRequestHeaders.Add("X-Forwarded-For", "10.36.0.6");

        var teacherUserId = await RegisterAsync(teacher, role: 2, label: "Hizli");
        var studentUserId = await RegisterAsync(studentClient, role: 3, label: "Ogrenci");
        var studentId = await CreateStudentAsync(teacher, teacherUserId, studentUserId);

        var opened = await teacher.PostAsJsonAsync("/api/messaging/conversations", new
        {
            counterpartUserId = Guid.Empty,
            counterpartRole = "Student",
            studentId,
        });
        var conversationId = (await ReadJsonAsync(opened)).GetProperty("id").GetGuid();
        var path = $"/api/messaging/conversations/{conversationId}/messages";

        for (var index = 1; index <= 30; index++)
        {
            var response = await teacher.PostAsJsonAsync(path, new { body = $"mesaj-{index}" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var limited = await teacher.PostAsJsonAsync(path, new { body = "mesaj-31" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("shared.rate_limited", await ErrorCodeAsync(limited));

        // Aynı IP'deki farklı kullanıcı kendi kotasına sahiptir: kota kullanıcı başınadır.
        var studentReply = await studentClient.PostAsJsonAsync(path, new { body = "Olur hocam." });
        Assert.Equal(HttpStatusCode.OK, studentReply.StatusCode);
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role, string label)
    {
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email = $"mesaj-{Guid.NewGuid():N}@test.com",
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

    /// <summary>
    /// Öğretmenin açtığı profil <c>Manual</c> bir <c>TeacherStudentLink</c> üretir; ilişki kapısının
    /// beklediği "yaşayan bağ" budur. <c>userId</c> dolu verilir ki öğrenci kendi adına konuşabilsin.
    /// </summary>
    private static async Task<Guid> CreateStudentAsync(
        HttpClient teacher, Guid teacherUserId, Guid studentUserId)
    {
        var response = await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = studentUserId,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName = "Mesaj Ogrencisi",
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin = 1,   // StudentOrigin.TeacherManaged
            subjects = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Outbox turu. Tek tur <b>yetmeyebilir</b>: işlemci her turda sınırlı bir parti (BatchSize)
    /// alır ve aynı Postgres'i paylaşan diğer testlerin bıraktığı satırlar partiyi doldurabilir.
    /// Bu yüzden beklenen bildirim düşene kadar sınırlı sayıda tur çevrilir.
    /// </summary>
    private static async Task DispatchOutboxUntilNotifiedAsync(
        WebApplicationFactory<Program> factory, Guid userId, int maxRounds = 10)
    {
        for (var round = 0; round < maxRounds; round++)
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync();
            }

            if (await CountNotificationsAsync(factory, userId) > 0)
            {
                return;
            }
        }

        Assert.Fail($"{maxRounds} outbox turunda bildirim üretilmedi.");
    }

    private static async Task DispatchOutboxAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOutboxProcessor>().DispatchPendingAsync();
    }

    private static async Task<int> CountNotificationsAsync(
        WebApplicationFactory<Program> factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        return await dbContext.Set<UserNotification>()
            .AsNoTracking()
            .CountAsync(row => row.UserId == userId);
    }

    private static async Task<UserNotification> SingleNotificationAsync(
        WebApplicationFactory<Program> factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        return await dbContext.Set<UserNotification>()
            .AsNoTracking()
            .SingleAsync(row => row.UserId == userId);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<List<JsonElement>> ReadListAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray().Select(row => row.Clone()).ToList();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
