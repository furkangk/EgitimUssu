using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Modules.Payments.Infrastructure;
using EgitimUssu.Modules.Settings.Infrastructure;
using EgitimUssu.Modules.Students.Infrastructure;
using EgitimUssu.Modules.Study.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P13 Task 5 / E-07: KVKK hesap silmenin <b>gerçek Postgres'te</b> uçtan uca doğrulaması.
/// <para>
/// Bu senaryo birim testiyle görülemez: kanıtlanan şey <b>on ayrı şemanın</b> tek bir akışta
/// gerçekten temizlenmesi. Bir modülün anonimleştiricisi DI'a kaydedilmemişse ya da yanlış kimliğe
/// (UserId ↔ StudentId asimetrisi) bakıyorsa, birim testi bunu göremez — sahte anonimleştiricilerle
/// koşar ve yeşil kalır. Burada gerçek kayıtlar yazılır ve gerçekten kaybolduğu okunur.
/// </para>
/// <para>
/// ⚠️ Zamanı ilerletmek için işlemci <b>elle</b> kurulur (sabit saat): 30 günlük bekleme süresi
/// gerçek saatle beklenemez. Kullanılan diğer her şey uygulamanın kendi DI kapsamından gelir,
/// yani anonimleştirici listesi de gerçek kayıtlardır.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealAccountDeletionTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Deletion_Should_Erase_Personal_Data_Across_Modules_But_Keep_Payment_Amounts()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var teacher = factory.CreateClient();
        teacher.DefaultRequestHeaders.Add("X-Forwarded-For", "10.44.0.1");
        var student = factory.CreateClient();
        student.DefaultRequestHeaders.Add("X-Forwarded-For", "10.44.0.2");

        var teacherUserId = await RegisterAsync(teacher, role: 2, label: "Silme-Ogretmen");
        var (studentUserId, studentEmail) = await RegisterWithEmailAsync(student, role: 3, label: "Silme-Ogrenci");
        var studentId = await CreateStudentAsync(factory, teacher, student, teacherUserId, studentUserId);

        // Öğrenci gerçek veri üretir: cihaz jetonu (push), ayar satırı, çalışma seansı.
        await RegisterDeviceAsync(student, studentUserId);
        await UpdateSettingsAsync(student, studentUserId);
        await StartAndFinishStudyAsync(student, studentId);

        // Öğretmen bir ücret kaydı açar — bu kayıt silinmemeli, yalnız kimliksizleşmeli.
        await CreatePaymentAsync(teacher, teacherUserId, studentId);

        // --- Silme öncesi: veriler gerçekten var mı? ---
        Assert.NotEmpty(await QueryAsync(factory, (NotificationsDbContext db) =>
            db.DeviceTokens.Where(token => token.UserId == studentUserId).ToArrayAsync()));
        Assert.NotEmpty(await QueryAsync(factory, (StudyDbContext db) =>
            db.StudySessions.Where(session => session.StudentId == studentId).ToArrayAsync()));
        Assert.NotEmpty(await QueryAsync(factory, (SettingsDbContext db) =>
            db.UserSettings.Where(setting => setting.UserId == studentUserId).ToArrayAsync()));

        var paymentsBefore = await QueryAsync(factory, (PaymentsDbContext db) =>
            db.PaymentRecords.Where(record => record.StudentId == studentId).ToArrayAsync());
        Assert.NotEmpty(paymentsBefore);
        var expectedTotal = paymentsBefore.Sum(record => record.ExpectedAmount);

        // --- Talep + bekleme süresi ---
        var request = await student.PostAsync("/api/identity/account/deletion-request", null);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var pending = await ReadJsonAsync(await student.GetAsync("/api/identity/account/deletion-request"));
        Assert.True(pending.GetProperty("pending").GetBoolean());

        // Bekleme süresi dolmadan hiçbir şey olmamalı.
        var early = await RunProcessorAsync(factory, DateTime.UtcNow.AddDays(29));
        Assert.Equal(0, early.CompletedCount);
        Assert.NotEmpty(await QueryAsync(factory, (NotificationsDbContext db) =>
            db.DeviceTokens.Where(token => token.UserId == studentUserId).ToArrayAsync()));

        // --- Süre dolunca uygulanır ---
        var summary = await RunProcessorAsync(factory, DateTime.UtcNow.AddDays(31));
        Assert.Equal(1, summary.CompletedCount);
        Assert.Equal(0, summary.FailedCount);

        // Identity: hesap kapalı, kimlik alanları gitti, eski e-posta ile giriş yok.
        var account = await QueryAsync(factory, (IdentityDbContext db) =>
            db.UserAccounts.SingleAsync(user => user.Id == studentUserId));
        Assert.Equal(UserAccountStatus.Closed, account.Status);
        Assert.Equal(PersonalDataPlaceholder.FullName, $"{account.FirstName} {account.LastName}");
        Assert.Null(account.PhoneNumber);
        Assert.DoesNotContain(studentEmail.Split('@')[0], account.Email, StringComparison.OrdinalIgnoreCase);

        var loginAfter = await factory.CreateClient().PostAsJsonAsync(
            "/api/identity/login", new { email = studentEmail, password = "Passw0rd!", deviceName = (string?)null });
        Assert.NotEqual(HttpStatusCode.OK, loginAfter.StatusCode);

        // Students: profil kimliksizleşti.
        var profile = await QueryAsync(factory, (StudentsDbContext db) =>
            db.StudentProfiles.SingleAsync(item => item.Id == studentId));
        Assert.Equal(PersonalDataPlaceholder.FullName, profile.FullName);
        Assert.Null(profile.ContactEmail);
        Assert.Null(profile.ContactPhone);

        // Notifications / Study / Settings: satırlar gitti (saklama amacı kalmadı).
        Assert.Empty(await QueryAsync(factory, (NotificationsDbContext db) =>
            db.DeviceTokens.Where(token => token.UserId == studentUserId).ToArrayAsync()));
        Assert.Empty(await QueryAsync(factory, (StudyDbContext db) =>
            db.StudySessions.Where(session => session.StudentId == studentId).ToArrayAsync()));
        Assert.Empty(await QueryAsync(factory, (SettingsDbContext db) =>
            db.UserSettings.Where(setting => setting.UserId == studentUserId).ToArrayAsync()));

        // Payments: TİCARİ KAYIT DURUYOR — tutar korunmuş, serbest metin gitmiş.
        var paymentsAfter = await QueryAsync(factory, (PaymentsDbContext db) =>
            db.PaymentRecords.Where(record => record.StudentId == studentId).ToArrayAsync());
        Assert.Equal(paymentsBefore.Length, paymentsAfter.Length);
        Assert.Equal(expectedTotal, paymentsAfter.Sum(record => record.ExpectedAmount));
        Assert.All(paymentsAfter, record => Assert.Equal(PersonalDataPlaceholder.RemovedContent, record.Description));

        // İkinci tur: idempotent, hiçbir şey değişmez.
        var again = await RunProcessorAsync(factory, DateTime.UtcNow.AddDays(32));
        Assert.Equal(0, again.CompletedCount);
    }

    // --- Yardımcılar ------------------------------------------------------------------

    /// <summary>
    /// İşlemciyi uygulamanın <b>gerçek</b> DI kapsamıyla, ama sabit bir saatle kurar: 30 günlük
    /// bekleme süresi gerçek saatle beklenemez. Anonimleştirici listesi gerçek kayıtlardan gelir.
    /// </summary>
    private static async Task<AccountDeletionRunSummary> RunProcessorAsync(
        WebApplicationFactory<Program> factory, DateTime nowUtc)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var processor = new AccountDeletionProcessor(
            services.GetRequiredService<IdentityDbContext>(),
            services.GetServices<IPersonalDataAnonymizer>(),
            services.GetRequiredService<IStudentDirectory>(),
            services.GetRequiredService<IPasswordHasher>(),
            services.GetRequiredService<IIdGenerator>(),
            new FixedClock(nowUtc),
            NullLogger<AccountDeletionProcessor>.Instance);

        return await processor.RunAsync();
    }

    private static async Task<T> QueryAsync<TContext, T>(
        WebApplicationFactory<Program> factory, Func<TContext, Task<T>> query)
        where TContext : DbContext
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<TContext>());
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, int role, string label)
        => (await RegisterWithEmailAsync(client, role, label)).UserId;

    private static async Task<(Guid UserId, string Email)> RegisterWithEmailAsync(
        HttpClient client, int role, string label)
    {
        var email = $"silme-{Guid.NewGuid():N}@test.com";
        var register = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email,
            password = "Passw0rd!",
            firstName = label,
            lastName = "Test",
            phoneNumber = "05551112233",
            roles = new[] { role },
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var payload = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", payload.RootElement.GetProperty("accessToken").GetString());
        return (payload.RootElement.GetProperty("userId").GetGuid(), email);
    }

    /// <summary>
    /// Öğretmen hesapsız profil açar, öğrenci <b>davet + claim</b> ile devralır (P15 T3 / N-23: gövdedeki
    /// <c>userId</c> artık reddedilir). Sonuç: profil öğrencinin, öğretmenle bağ <c>Linked</c>.
    /// </summary>
    private static async Task<Guid> CreateStudentAsync(
        WebApplicationFactory<Program> factory, HttpClient teacher, HttpClient student, Guid teacherUserId, Guid studentUserId)
    {
        var response = await teacher.PostAsJsonAsync("/api/students/profiles", new
        {
            userId = (Guid?)null,
            createdByTeacherUserId = teacherUserId,
            parentUserId = (Guid?)null,
            fullName = "Silme Ogrencisi",
            gradeLevel = "8. Sınıf",
            contactEmail = "veli@example.com",
            contactPhone = "05559998877",
            goalSummary = "Hedef: 450 net",
            levelNotes = (string?)null,
            origin = 1,
            subjects = Array.Empty<object>(),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var studentId = (await ReadJsonAsync(response)).GetProperty("id").GetGuid();
        await StudentLinkTestHelper.LinkStudentAccountAsync(factory, teacher, student, teacherUserId, studentId, studentUserId);
        return studentId;
    }

    private static async Task RegisterDeviceAsync(HttpClient student, Guid studentUserId)
    {
        var response = await student.PostAsJsonAsync("/api/notifications/device-tokens", new
        {
            userId = studentUserId,
            token = $"fake-fcm-{Guid.NewGuid():N}",
            platform = 1,
            deviceName = "iPhone 15",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task UpdateSettingsAsync(HttpClient student, Guid studentUserId)
    {
        var response = await student.PutAsJsonAsync($"/api/settings/users/{studentUserId}", new
        {
            pushNotificationsEnabled = true,
            emailNotificationsEnabled = true,
            upcomingLessonReminderEnabled = true,
            homeworkReminderEnabled = true,
            paymentReminderEnabled = true,
            weeklySummaryEnabled = true,
            privacyLevel = 1,
            sessionTerminationPolicy = 1,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task StartAndFinishStudyAsync(HttpClient student, Guid studentId)
    {
        var response = await student.PostAsJsonAsync("/api/study/sessions/manual", new
        {
            studentId,
            subject = "Matematik",
            topic = "Türev",
            effectiveMinutes = 45,
            studiedOnUtc = DateTime.UtcNow.AddHours(-2),
            personalNote = "Zorlandım",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task CreatePaymentAsync(HttpClient teacher, Guid teacherUserId, Guid studentId)
    {
        var response = await teacher.PostAsJsonAsync("/api/payments/records", new
        {
            teacherUserId,
            studentId,
            relatedLessonSessionId = (Guid?)null,
            itemType = 1,
            description = "Silme Ogrencisi - Eylul dersleri",
            currency = "TRY",
            expectedAmount = 1250m,
            collectedAmount = 0m,
            dueDateUtc = DateTime.UtcNow.AddDays(7),
            collectedOnUtc = (DateTime?)null,
            status = 1,
            billingPeriodStartUtc = (DateTime?)null,
            billingPeriodEndUtc = (DateTime?)null,
            notes = "Veli elden odeyecek",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
