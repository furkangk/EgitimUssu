using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.ProgressTracking.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P15 Task 9: Çapraz-kiracı yetki paketi — gerçek Postgres + gerçek DI + gerçek HTTP hata eşlemesi.
/// <para>
/// İki "kiracı" kurulur: her birinde bir öğretmen, onun davetle hesabına bağladığı bir öğrenci ve öğretmenin veli
/// davetini kullanan bir veli; öğretmenin bu öğrenci için planlı dersi, ders oturumu, ödeme kaydı ve sohbeti vardır.
/// Saldırgan her zaman <b>kendi kimliğiyle</b> ve kurbanın kaynak kimliğiyle ister (T2 deseni). Her ret, aynı çağrının
/// sahibince başarılı olmasıyla eşlenir (pozitif kontrol, Öğrenilenler #196) — anlamsız bir 403 yeşil sayılmasın.
/// </para>
/// <para>
/// Bağı arşivlenmiş öğretmen senaryoları T2'den devreden #185'i kapatır: otomatik ders ücreti yalnız yaşayan özel ders
/// bağında oluşur; yeni veli bağı (<c>link-parent</c>) yaşayan bağ ister; arşivden önce açılmış kaydı kapatmak
/// (oturum türetme/tamamlama, ödev takibi) sahiplikle sürer.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealCrossTenantAccessTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Teacher_Cannot_Write_To_Another_Teachers_Student()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var a = await BuildTenantAsync(factory, "10.160.0", "KiraciA", dayOffset: 1);
        var b = await BuildTenantAsync(factory, "10.160.1", "KiraciB", dayOffset: 2);

        // Ders, ödeme, oturum: yeni kayıt (N-08) ve var olan kayıt üzerinde değişiklik.
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateLessonAsync(b.Teacher, b.TeacherUserId, a.StudentId, dayOffset: 3)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreatePaymentAsync(b.Teacher, b.TeacherUserId, a.StudentId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateSessionAsync(b.Teacher, b.TeacherUserId, a.StudentId, lessonScheduleId: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Teacher.PostAsync($"/api/lesson-sessions/from-schedule/{a.LessonId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CompleteSessionAsync(b.Teacher, a.SessionId, isChargeable: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Teacher.PostAsync($"/api/lesson-sessions/{a.SessionId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CancelLessonAsync(b.Teacher, a.LessonId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UpdatePaymentAsync(b.Teacher, a.PaymentId, b.TeacherUserId, a.StudentId, expectedAmount: 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateFollowUpAsync(b.Teacher, a.SessionId)).StatusCode);

        // Veli bağı (N-21) ve profil.
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateParentInviteResponseAsync(b.Teacher, a.StudentId, b.TeacherUserId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await LinkParentAsync(b.Teacher, a.StudentId, b.ParentUserId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UpdateProfileAsync(b.Teacher, a.StudentId, "Ele Gecirilmis")).StatusCode);

        // Bağ uçları: B kendi kimliğiyle A'nın öğrencisinde bağ bulamaz; A'nın kimliğini taşıyamaz.
        var rateWithOwnId = await SetRateAsync(b.Teacher, b.TeacherUserId, a.StudentId, 1m);
        Assert.Equal(HttpStatusCode.NotFound, rateWithOwnId.StatusCode);
        Assert.Equal("students.link_not_found", await ErrorCodeAsync(rateWithOwnId));
        Assert.Equal(HttpStatusCode.Forbidden, (await SetRateAsync(b.Teacher, a.TeacherUserId, a.StudentId, 1m)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Teacher.PostAsync(
            $"/api/students/teachers/{a.TeacherUserId}/students/{a.StudentId}/archive", content: null)).StatusCode);

        // Mesaj (N-46): ilişki yoksa sohbet açılmaz; başkasının sohbetine yazılmaz.
        var openWithStudent = await OpenConversationAsync(b.Teacher, Guid.Empty, "Student", a.StudentId);
        Assert.Equal(HttpStatusCode.Conflict, openWithStudent.StatusCode);
        Assert.Equal("messaging.relationship_required", await ErrorCodeAsync(openWithStudent));
        var openWithParent = await OpenConversationAsync(b.Teacher, a.ParentUserId, "Parent", a.StudentId);
        Assert.Equal(HttpStatusCode.Conflict, openWithParent.StatusCode);
        Assert.Equal("messaging.relationship_required", await ErrorCodeAsync(openWithParent));
        Assert.Equal(HttpStatusCode.Forbidden, (await SendMessageAsync(b.Teacher, a.ConversationId, "Merhaba")).StatusCode);

        // Pozitif kontrol: aynı gövdelerle sahibi yazabilir; A'nın kayıtları B'nin denemelerinden etkilenmedi.
        Assert.Equal(HttpStatusCode.OK, (await UpdatePaymentAsync(a.Teacher, a.PaymentId, a.TeacherUserId, a.StudentId, expectedAmount: 1000)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SetRateAsync(a.Teacher, a.TeacherUserId, a.StudentId, 500m)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await UpdateProfileAsync(a.Teacher, a.StudentId, "Ada Sahin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendMessageAsync(a.Teacher, a.ConversationId, "Merhaba Ada")).StatusCode);
        var session = await ReadJsonAsync(await a.Teacher.GetAsync($"/api/lesson-sessions/{a.SessionId}"));
        Assert.Equal("Planned", session.GetProperty("status").GetString());
        var lesson = await ReadJsonAsync(await a.Teacher.GetAsync($"/api/scheduling/lessons/{a.LessonId}"));
        Assert.NotEqual("Cancelled", lesson.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task Teacher_Cannot_Read_Another_Teachers_Student()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var a = await BuildTenantAsync(factory, "10.160.2", "OkuA", dayOffset: 1);
        var b = await BuildTenantAsync(factory, "10.160.3", "OkuB", dayOffset: 2);

        var reads = new[]
        {
            $"/api/students/profiles/{a.StudentId}",
            $"/api/students/profiles/by-teacher/{a.TeacherUserId}",
            $"/api/payments/records/{a.PaymentId}",
            $"/api/payments/teachers/{a.TeacherUserId}/records?outstandingOnly=false",
            $"/api/payments/teachers/{a.TeacherUserId}/summary",
            $"/api/lesson-sessions/{a.SessionId}",
            $"/api/lesson-sessions?teacherUserId={a.TeacherUserId}",
            $"/api/scheduling/lessons/{a.LessonId}",
            $"/api/messaging/conversations/{a.ConversationId}/messages",
        };

        foreach (var path in reads)
        {
            Assert.True((await a.Teacher.GetAsync(path)).StatusCode == HttpStatusCode.OK, $"sahibi okuyamadı: {path}");
            var denied = await b.Teacher.GetAsync(path);
            Assert.True(denied.StatusCode == HttpStatusCode.Forbidden, $"{path}: {(int)denied.StatusCode}");
        }

        // Öğrenci kapsamlı ders listesi yalnız öğrencinin kendisine açık.
        Assert.Equal(HttpStatusCode.OK, (await a.Student.GetAsync(StudentLessonsPath(a.StudentId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Teacher.GetAsync(StudentLessonsPath(a.StudentId))).StatusCode);

        // Liste filtresi istemciye güvenmez: B, A'nın öğrencisini süzgece koysa da yalnız kendi oturumlarını görür (Y2/K2).
        var filtered = await b.Teacher.GetAsync($"/api/lesson-sessions?studentId={a.StudentId}");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.DoesNotContain(a.SessionId.ToString(), await filtered.Content.ReadAsStringAsync());
        Assert.Contains(b.SessionId.ToString(), await ReadBodyAsync(b.Teacher, $"/api/lesson-sessions?studentId={b.StudentId}"));

        // Sohbet listesi yalnız katılımcının sohbetleri (pozitif kontrol: B kendi sohbetini görür).
        var inbox = await ReadBodyAsync(b.Teacher, "/api/messaging/conversations");
        Assert.Contains(b.ConversationId.ToString(), inbox);
        Assert.DoesNotContain(a.ConversationId.ToString(), inbox);
    }

    [SkippableFact]
    public async Task Parent_Cannot_See_Another_Parents_Child()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var a = await BuildTenantAsync(factory, "10.160.4", "VeliA", dayOffset: 1);
        var b = await BuildTenantAsync(factory, "10.160.5", "VeliB", dayOffset: 2);

        // Profil ve pano: A'nın velisi görür, B'nin velisi kendi kimliğiyle de göremez.
        Assert.Equal(HttpStatusCode.OK, (await a.Parent.GetAsync($"/api/students/profiles/{a.StudentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Parent.GetAsync($"/api/students/profiles/{a.StudentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Parent.GetAsync($"/api/parents/{a.ParentUserId}/children/{a.StudentId}/dashboard")).StatusCode);
        var dashboard = await b.Parent.GetAsync($"/api/parents/{b.ParentUserId}/children/{a.StudentId}/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, dashboard.StatusCode);

        // Çocuk listeleri: başkasının listesi istenemez; kendi listesinde başkasının çocuğu yok.
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Parent.GetAsync($"/api/parents/{a.ParentUserId}/children")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Parent.GetAsync($"/api/students/profiles/by-parent/{a.ParentUserId}")).StatusCode);
        var ownChildren = await ReadBodyAsync(b.Parent, $"/api/parents/{b.ParentUserId}/children");
        Assert.Contains(b.StudentId.ToString(), ownChildren);
        Assert.DoesNotContain(a.StudentId.ToString(), ownChildren);

        // Öğrenci kapsamlı ders listesi veliye de açık değil.
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Parent.GetAsync(StudentLessonsPath(a.StudentId))).StatusCode);

        // Yazma: başkasının çocuğunun borcu "ödendi" beyan edilemez, öğretmeniyle sohbet açılamaz.
        Assert.Equal(HttpStatusCode.Forbidden, (await DeclarePaidAsync(b.Parent, a.PaymentId)).StatusCode);
        var open = await OpenConversationAsync(b.Parent, a.TeacherUserId, "Teacher", a.StudentId);
        Assert.Equal(HttpStatusCode.Conflict, open.StatusCode);
        Assert.Equal("messaging.relationship_required", await ErrorCodeAsync(open));

        // Pozitif kontrol: aynı çağrılar A'nın velisinde geçer.
        Assert.Equal(HttpStatusCode.OK, (await DeclarePaidAsync(a.Parent, a.PaymentId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await OpenConversationAsync(a.Parent, a.TeacherUserId, "Teacher", a.StudentId)).StatusCode);
    }

    [SkippableFact]
    public async Task Revoked_Parent_Can_No_Longer_See_The_Child()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var a = await BuildTenantAsync(factory, "10.160.6", "IptalA", dayOffset: 1);

        var profilePath = $"/api/students/profiles/{a.StudentId}";
        var dashboardPath = $"/api/parents/{a.ParentUserId}/children/{a.StudentId}/dashboard";
        var studentListPath = $"/api/students/profiles/by-parent/{a.ParentUserId}";
        var childListPath = $"/api/parents/{a.ParentUserId}/children";
        Assert.Equal(HttpStatusCode.OK, (await a.Parent.GetAsync(profilePath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Parent.GetAsync(dashboardPath)).StatusCode);
        Assert.Contains(a.StudentId.ToString(), await ReadBodyAsync(a.Parent, studentListPath));
        Assert.Equal("Approved", (await ReadChildRowAsync(a.Parent, childListPath, a.StudentId)).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.OK, (await a.Parent.PostAsync($"/api/parents/children/{a.ParentLinkId}/revoke", null)).StatusCode);

        // İptal anında (olay işlenmeden) ve olay işlendikten sonra aynı sonuç.
        foreach (var round in new[] { "iptal anı", "olay işlendi" })
        {
            Assert.True((await a.Parent.GetAsync(profilePath)).StatusCode == HttpStatusCode.Forbidden, $"{round}: profil");
            Assert.True((await a.Parent.GetAsync(dashboardPath)).StatusCode == HttpStatusCode.Forbidden, $"{round}: pano");
            Assert.True((await DeclarePaidAsync(a.Parent, a.PaymentId)).StatusCode == HttpStatusCode.Forbidden, $"{round}: ödeme beyanı");
            Assert.DoesNotContain(a.StudentId.ToString(), await ReadBodyAsync(a.Parent, studentListPath));
            // Velinin kendi çocuk listesi bağ GEÇMİŞİNİ de döner (tasarım): satır kalır, ama iptal edilmiş ve çocuğa ait
            // gelişim özeti taşımaz — özet yalnız onaylı bağda (ListChildrenQueryHandler).
            var row = await ReadChildRowAsync(a.Parent, childListPath, a.StudentId);
            Assert.True(row.GetProperty("status").GetString() == "Revoked", $"{round}: bağ durumu");
            Assert.True(row.GetProperty("progress").ValueKind == JsonValueKind.Null, $"{round}: gelişim özeti");
            var open = await OpenConversationAsync(a.Parent, a.TeacherUserId, "Teacher", a.StudentId);
            Assert.True(open.StatusCode == HttpStatusCode.Conflict, $"{round}: sohbet {(int)open.StatusCode}");

            await DrainOutboxAsync(factory);
        }
    }

    /// <summary>
    /// #185 (a): Bağı arşivlenmiş öğretmen, bağlıyken açtığı plandan oturum türetip ücretli tamamlayabilir (eski kaydı
    /// kapatmak), ama velinin paneline otomatik borç düşmez — manuel ödeme kaydında olduğu gibi (T2) para hareketi yalnız
    /// yaşayan özel ders bağında (K-18). Pozitif kontrol aynı öğretmen, aynı ücret, aynı akış: bağlıyken ücret oluşur.
    /// </summary>
    [SkippableFact]
    public async Task Archived_Teacher_Completing_From_An_Old_Schedule_Creates_No_Lesson_Fee()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.160.7.1");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Arsiv-Ucret-Ogretmen");
        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId, "Arsiv Ogrenci");
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await SetRateAsync(teacher, teacherUserId, studentId, 750m)).StatusCode);
            var linkedLessonId = await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, dayOffset: 1));
            var archivedLessonId = await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, dayOffset: 2));

            // Bağlıyken: ücretli tamamlama → otomatik ders ücreti (M07-2).
            var linkedSessionId = await ReadIdAsync(await teacher.PostAsync($"/api/lesson-sessions/from-schedule/{linkedLessonId}", null));
            Assert.Equal(HttpStatusCode.OK, (await CompleteSessionAsync(teacher, linkedSessionId, isChargeable: true)).StatusCode);
            await DrainOutboxAsync(factory);
            var linkedFee = Assert.Single(await ReadLessonFeesAsync(factory, linkedSessionId));
            Assert.Equal(750m, linkedFee);

            Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync(
                $"/api/students/teachers/{teacherUserId}/students/{studentId}/archive", content: null)).StatusCode);

            // Arşivden sonra: eski plandan oturum türetmek ve tamamlamak serbest (kaydı kapatma)...
            var archivedSession = await teacher.PostAsync($"/api/lesson-sessions/from-schedule/{archivedLessonId}", null);
            Assert.Equal(HttpStatusCode.OK, archivedSession.StatusCode);
            var archivedSessionId = await ReadIdAsync(archivedSession);
            Assert.Equal(HttpStatusCode.OK, (await CompleteSessionAsync(teacher, archivedSessionId, isChargeable: true)).StatusCode);
            await DrainOutboxAsync(factory);

            // ...ama velinin paneline borç düşmez.
            Assert.Empty(await ReadLessonFeesAsync(factory, archivedSessionId));
        }
        finally
        {
            await DeleteProgressRowsAsync(factory, studentId);
        }
    }

    /// <summary>
    /// #185 (b)/(c) kararı: yeni veli bağı açmak (<c>link-parent</c>) kardeşi <c>parent-invite</c> (N-21) gibi yaşayan bağ
    /// ister — üç yetki senaryosu: başka öğretmen, bağı arşivlenmiş öğretmen, bağsız kullanıcı. Arşivden önce yapılmış dersin
    /// ödev takibi ise sahiplikle sürer (kaydı kapatma; para ya da yeni ilişki doğurmaz).
    /// </summary>
    [SkippableFact]
    public async Task Archived_Teacher_Cannot_Open_A_Parent_Link_But_Can_Close_Out_Homework()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = NewClient(factory, "10.160.8.1");
        var otherTeacher = NewClient(factory, "10.160.8.2");
        var unrelatedStudent = NewClient(factory, "10.160.8.3");
        var firstParent = NewClient(factory, "10.160.8.4");
        var secondParent = NewClient(factory, "10.160.8.5");
        var teacherUserId = await RegisterAsync(teacher, role: 2, "Arsiv-Veli-Ogretmen");
        await RegisterAsync(otherTeacher, role: 2, "Arsiv-Veli-Baska-Ogretmen");
        await RegisterAsync(unrelatedStudent, role: 3, "Arsiv-Veli-Bagsiz");
        var firstParentUserId = await RegisterParentWithProfileAsync(firstParent, "Arsiv-Veli-Bir");
        var secondParentUserId = await RegisterParentWithProfileAsync(secondParent, "Arsiv-Veli-Iki");
        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId, "Arsiv Veli Ogrenci");
        // Bağlıyken yapılmış (tamamlanmış, ücretsiz, plansız) ders — takip yalnız tamamlanmış oturuma yazılır.
        var sessionId = await ReadIdAsync(await CreateSessionAsync(teacher, teacherUserId, studentId, lessonScheduleId: null));
        Assert.Equal(HttpStatusCode.OK, (await CompleteSessionAsync(teacher, sessionId, isChargeable: false)).StatusCode);

        // Pozitif kontrol: bağlı öğretmen veli bağını açar (Pending; onay velide/öğrencide).
        var linked = await LinkParentAsync(teacher, studentId, firstParentUserId);
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal("Pending", (await ReadJsonAsync(linked)).GetProperty("status").GetString());

        // Başka öğretmen ve bağsız kullanıcı açamaz.
        Assert.Equal(HttpStatusCode.Forbidden, (await LinkParentAsync(otherTeacher, studentId, secondParentUserId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await LinkParentAsync(unrelatedStudent, studentId, secondParentUserId)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync(
            $"/api/students/teachers/{teacherUserId}/students/{studentId}/archive", content: null)).StatusCode);

        // Bağı arşivlenmiş öğretmen yeni veli bağı açamaz (parent-invite ile aynı kural).
        var archived = await LinkParentAsync(teacher, studentId, secondParentUserId);
        Assert.Equal(HttpStatusCode.Forbidden, archived.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateParentInviteResponseAsync(teacher, studentId, teacherUserId)).StatusCode);

        // Arşivden önce açılmış oturumun ödev takibi sahiplikle sürer (karakterizasyon).
        Assert.Equal(HttpStatusCode.OK, (await CreateFollowUpAsync(teacher, sessionId)).StatusCode);
    }

    private sealed record Tenant(
        HttpClient Teacher,
        Guid TeacherUserId,
        HttpClient Student,
        Guid StudentUserId,
        Guid StudentId,
        HttpClient Parent,
        Guid ParentUserId,
        Guid ParentLinkId,
        Guid LessonId,
        Guid SessionId,
        Guid PaymentId,
        Guid ConversationId);

    /// <summary>
    /// Bir kiracı: öğretmenin açtığı profil, davetle bağlanan öğrenci hesabı, veli daveti ile bağlanan veli; planlı ders,
    /// o derse bağlı oturum, ödeme kaydı ve öğretmen–öğrenci sohbeti. Her istemci kendi hız-sınırı bölmesindedir.
    /// </summary>
    private static async Task<Tenant> BuildTenantAsync(WebApplicationFactory<Program> factory, string subnet, string label, int dayOffset)
    {
        var teacher = NewClient(factory, $"{subnet}.1");
        var student = NewClient(factory, $"{subnet}.2");
        var parent = NewClient(factory, $"{subnet}.3");
        var teacherUserId = await RegisterAsync(teacher, role: 2, $"{label}-Ogretmen");
        var studentUserId = await RegisterAsync(student, role: 3, $"{label}-Ogrenci");
        var parentUserId = await RegisterAsync(parent, role: 4, $"{label}-Veli");

        var studentId = await CreateTeacherProfileAsync(teacher, teacherUserId, $"{label} Ogrenci");
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);
        var inviteCode = (await ReadJsonAsync(await CreateParentInviteResponseAsync(teacher, studentId, teacherUserId)))
            .GetProperty("inviteCode").GetString()!;
        var claim = await parent.PostAsJsonAsync("/api/parents/children/claim-invite", new { inviteCode });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var parentLinkId = (await ReadJsonAsync(claim)).GetProperty("id").GetGuid();
        await DrainOutboxAsync(factory);

        var lessonId = await ReadIdAsync(await CreateLessonAsync(teacher, teacherUserId, studentId, dayOffset));
        var sessionId = await ReadIdAsync(await CreateSessionAsync(teacher, teacherUserId, studentId, lessonId));
        var paymentId = await ReadIdAsync(await CreatePaymentAsync(teacher, teacherUserId, studentId));
        var conversation = await OpenConversationAsync(teacher, Guid.Empty, "Student", studentId);
        Assert.Equal(HttpStatusCode.OK, conversation.StatusCode);
        var conversationId = (await ReadJsonAsync(conversation)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SendMessageAsync(teacher, conversationId, "Merhaba")).StatusCode);

        return new Tenant(
            teacher, teacherUserId, student, studentUserId, studentId, parent, parentUserId, parentLinkId,
            lessonId, sessionId, paymentId, conversationId);
    }

    /// <summary>Pencere parametreleri zorunlu (varsayılansız sorgu parametresi eksikse minimal API 500 döner — #85).</summary>
    private static string StudentLessonsPath(Guid studentId)
    {
        var from = DateTime.UtcNow.Date.AddDays(-1).ToString("O");
        var to = DateTime.UtcNow.Date.AddDays(14).ToString("O");
        return $"/api/scheduling/students/{studentId}/lessons?startAtUtc={Uri.EscapeDataString(from)}&endAtUtc={Uri.EscapeDataString(to)}";
    }

    private static Task<HttpResponseMessage> CreateLessonAsync(HttpClient client, Guid teacherUserId, Guid studentId, int dayOffset)
    {
        var startAtUtc = DateTime.UtcNow.Date.AddDays(dayOffset).AddHours(15);
        return client.PostAsJsonAsync("/api/scheduling/lessons", new
        {
            teacherUserId,
            studentId,
            subject = "Matematik",
            lessonFormat = 2,
            startAtUtc,
            endAtUtc = startAtUtc.AddHours(1),
            timeZone = "Europe/Istanbul",
            recurrenceRule = (string?)null,
            reminderOffsetMinutes = 60,
            locationLabel = "Zoom",
            notes = (string?)null,
        });
    }

    private static Task<HttpResponseMessage> CancelLessonAsync(HttpClient client, Guid lessonId)
        => client.PostAsJsonAsync($"/api/scheduling/lessons/{lessonId}/cancel", new
        {
            reason = 1,
            isChargeable = false,
            cancellationNote = (string?)null,
        });

    private static Task<HttpResponseMessage> CreateSessionAsync(HttpClient client, Guid teacherUserId, Guid studentId, Guid? lessonScheduleId)
        => client.PostAsJsonAsync("/api/lesson-sessions", new
        {
            lessonScheduleId,
            teacherUserId,
            studentId,
            subject = "Matematik",
            plannedStartAtUtc = DateTime.UtcNow.Date.AddDays(1).AddHours(15),
            topicTitle = "Problemler",
        });

    private static Task<HttpResponseMessage> CompleteSessionAsync(HttpClient client, Guid sessionId, bool isChargeable)
    {
        var startAtUtc = DateTime.UtcNow.Date.AddHours(9);
        return client.PostAsJsonAsync($"/api/lesson-sessions/{sessionId}/complete", new
        {
            actualStartAtUtc = startAtUtc,
            actualEndAtUtc = startAtUtc.AddHours(1),
            attendanceStatus = 2,
            topicTitle = "Problemler",
            coveredContent = "Oran oranti",
            teacherNotes = "Iyi gecti",
            isChargeable,
        });
    }

    private static Task<HttpResponseMessage> CreateFollowUpAsync(HttpClient client, Guid sessionId)
        => client.PostAsJsonAsync($"/api/assignments/lesson-sessions/{sessionId}/follow-up", new
        {
            summary = "Problemler konusu tekrar edildi.",
            coveredTopics = "Oran oranti",
            recommendations = "Her gun 20 soru.",
            assignments = new[]
            {
                new
                {
                    title = "Problem foyu",
                    description = "Tum sorular cozulecek",
                    dueDateUtc = DateTime.UtcNow.Date.AddDays(3),
                    attachmentUrl = (string?)null,
                },
            },
        });

    private static Task<HttpResponseMessage> CreatePaymentAsync(HttpClient client, Guid teacherUserId, Guid studentId)
        => client.PostAsJsonAsync("/api/payments/records", PaymentBody(teacherUserId, studentId, expectedAmount: 1000));

    private static Task<HttpResponseMessage> UpdatePaymentAsync(
        HttpClient client, Guid paymentId, Guid teacherUserId, Guid studentId, decimal expectedAmount)
        => client.PutAsJsonAsync($"/api/payments/records/{paymentId}", PaymentBody(teacherUserId, studentId, expectedAmount));

    private static object PaymentBody(Guid teacherUserId, Guid studentId, decimal expectedAmount) => new
    {
        teacherUserId,
        studentId,
        relatedLessonSessionId = (Guid?)null,
        itemType = 1,
        description = "Ders ucreti",
        currency = "TRY",
        expectedAmount,
        collectedAmount = 0,
        dueDateUtc = DateTime.UtcNow.AddDays(3),
        collectedOnUtc = (DateTime?)null,
        status = 1,
        billingPeriodStartUtc = (DateTime?)null,
        billingPeriodEndUtc = (DateTime?)null,
        notes = (string?)null,
    };

    private static Task<HttpResponseMessage> DeclarePaidAsync(HttpClient parent, Guid paymentId)
        => parent.PostAsJsonAsync($"/api/payments/records/{paymentId}/declare-paid", new { declaredAmount = 1000m, note = (string?)null });

    private static Task<HttpResponseMessage> SetRateAsync(HttpClient client, Guid teacherUserId, Guid studentId, decimal amount)
        => client.PutAsJsonAsync(
            $"/api/students/teachers/{teacherUserId}/students/{studentId}/rate",
            new { agreedRateAmount = amount, currency = "TRY" });

    private static Task<HttpResponseMessage> CreateParentInviteResponseAsync(HttpClient teacher, Guid studentId, Guid teacherUserId)
        => teacher.PostAsJsonAsync(
            $"/api/students/profiles/{studentId}/parent-invite",
            new { teacherUserId, childDisplayName = "Ada" });

    private static Task<HttpResponseMessage> LinkParentAsync(HttpClient client, Guid studentId, Guid parentUserId)
        => client.PostAsJsonAsync($"/api/students/profiles/{studentId}/link-parent", new { parentUserId });

    private static Task<HttpResponseMessage> UpdateProfileAsync(HttpClient client, Guid studentId, string fullName)
        => client.PutAsJsonAsync($"/api/students/profiles/{studentId}", new
        {
            fullName,
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            isActive = true,
            subjects = Array.Empty<object>(),
        });

    private static Task<HttpResponseMessage> OpenConversationAsync(HttpClient client, Guid counterpartUserId, string counterpartRole, Guid studentId)
        => client.PostAsJsonAsync("/api/messaging/conversations", new { counterpartUserId, counterpartRole, studentId });

    private static Task<HttpResponseMessage> SendMessageAsync(HttpClient client, Guid conversationId, string body)
        => client.PostAsJsonAsync($"/api/messaging/conversations/{conversationId}/messages", new { body });

    private static async Task<Guid> CreateTeacherProfileAsync(HttpClient teacher, Guid teacherUserId, string fullName)
    {
        var response = await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName,
            gradeLevel = "8. Sınıf",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            goalSummary = (string?)null,
            levelNotes = (string?)null,
            origin = 1,   // StudentOrigin.TeacherManaged
            subjects = Array.Empty<object>(),
        });
        return await ReadIdAsync(response);
    }

    /// <summary><c>link-parent</c> yalnız Parents'ta profili olan veliye bağ açar (<c>students.parent_user_required</c>).</summary>
    private static async Task<Guid> RegisterParentWithProfileAsync(HttpClient parent, string label)
    {
        var parentUserId = await RegisterAsync(parent, role: 4, label);
        var profile = await parent.PostAsJsonAsync("/api/parents/profiles", new
        {
            userId = parentUserId,
            fullName = $"{label} Test",
            contactPhone = (string?)null,
            contactEmail = (string?)null,
        });
        Assert.True(profile.IsSuccessStatusCode, $"veli profili: {(int)profile.StatusCode}");
        return parentUserId;
    }

    private static async Task<IReadOnlyList<decimal>> ReadLessonFeesAsync(WebApplicationFactory<Program> factory, Guid sessionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().PaymentRecords.AsNoTracking()
            .Where(record => record.RelatedLessonSessionId == sessionId)
            .Select(record => record.ExpectedAmount)
            .ToListAsync();
    }

    /// <summary>
    /// Tamamlanan planlı ders hâkimiyet satırı yazar; <c>ProgressSnapshotProcessor</c> paylaşılan Postgres'teki bütün
    /// satırları tarar (Öğrenilenler #188) — bu testin satırları kalmamalı.
    /// </summary>
    private static async Task DeleteProgressRowsAsync(WebApplicationFactory<Program> factory, Guid studentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var progress = scope.ServiceProvider.GetRequiredService<ProgressTrackingDbContext>();
        await progress.TopicMasteries.Where(x => x.StudentId == studentId).ExecuteDeleteAsync();
        await progress.ProgressSnapshots.Where(x => x.StudentId == studentId).ExecuteDeleteAsync();
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
            email = $"kiraci-{Guid.NewGuid():N}@test.com",
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

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Gövdeyi okur ve önce 200'ü doğrular — "içermiyor" kontrolü hata yanıtında boş geçmesin (#196).</summary>
    private static async Task<string> ReadBodyAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");
        return body;
    }

    private static async Task<JsonElement> ReadChildRowAsync(HttpClient parent, string path, Guid studentId)
    {
        using var document = JsonDocument.Parse(await ReadBodyAsync(parent, path));
        return document.RootElement.EnumerateArray()
            .Single(row => row.GetProperty("studentId").GetGuid() == studentId)
            .Clone();
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
