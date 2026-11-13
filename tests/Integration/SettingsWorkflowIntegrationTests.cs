using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimUssu.Modules.Notifications.Domain;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Push;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// M15-1/M15-2: Kullanıcı ayarları uçlarının uçtan uca davranışı.
/// </summary>
public sealed class SettingsWorkflowIntegrationTests
{
    [Fact]
    public async Task Settings_Should_Default_Then_Persist_And_Deny_Other_Users()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "InMemory:settings-workflow-tests");

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            var (userId, token) = await RegisterAsync(client, "settings-owner@example.com");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Kayıt yokken varsayılan ayar 200 ile dönmeli (404 değil).
            var defaultResponse = await client.GetAsync($"/api/settings/users/{userId}");
            Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
            var defaults = await defaultResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(defaults.GetProperty("homeworkReminderEnabled").GetBoolean());
            Assert.Equal("Standard", defaults.GetProperty("privacyLevel").GetString());
            Assert.Equal("KeepLatest", defaults.GetProperty("sessionTerminationPolicy").GetString());

            // Ödev hatırlatmasını kapat.
            var updateResponse = await client.PutAsJsonAsync($"/api/settings/users/{userId}", new
            {
                pushNotificationsEnabled = true,
                emailNotificationsEnabled = true,
                upcomingLessonReminderEnabled = true,
                homeworkReminderEnabled = false,
                paymentReminderEnabled = true,
                weeklySummaryEnabled = true,
                privacyLevel = 2,
                sessionTerminationPolicy = 2
            });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            // Okuma değişikliği yansıtmalı.
            var readBack = await client.GetAsync($"/api/settings/users/{userId}");
            readBack.EnsureSuccessStatusCode();
            var stored = await readBack.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(stored.GetProperty("homeworkReminderEnabled").GetBoolean());
            Assert.Equal("Limited", stored.GetProperty("privacyLevel").GetString());
            Assert.Equal("TerminateOtherSessions", stored.GetProperty("sessionTerminationPolicy").GetString());

            // Başka bir kullanıcının ayarına erişim yasak.
            var (otherUserId, _) = await RegisterAsync(client, "settings-other@example.com");
            var forbidden = await client.GetAsync($"/api/settings/users/{otherUserId}");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", previousConnectionString);
        }
    }


    /// <summary>
    /// P05 kabul kriteri: kapatılan bildirim türü **gerçekten gönderilmiyor**.
    /// Zincirin tamamı: `PUT /api/settings` → `UserNotificationPreferencesDirectory`
    /// → `NotificationDispatchProcessor` → `IPushSender`.
    /// </summary>
    [Fact]
    public async Task Disabled_Homework_Preference_Should_Stop_Push_End_To_End()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "InMemory:settings-preference-gate-tests");

        try
        {
            var pushSender = new RecordingPushSender();
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IPushSender>(pushSender);
                }));

            using var client = factory.CreateClient();
            var (userId, token) = await RegisterAsync(client, "settings-gate@example.com");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Kullanıcının bir cihazı olsun, yoksa push zaten atlanır ve test bir şey kanıtlamaz.
            var registerToken = await client.PostAsJsonAsync("/api/notifications/device-tokens", new
            {
                userId,
                token = "gate-test-token",
                platform = 1,
                deviceName = "Pixel"
            });
            registerToken.EnsureSuccessStatusCode();

            // Önce tercih AÇIKKEN: ödev bildirimi push ediliyor mu?
            await AddUserNotificationAsync(factory, userId, UserNotificationKind.AssignmentCreated);
            await DispatchAsync(factory);
            Assert.Single(pushSender.SentTokens);

            // Şimdi ödev hatırlatmalarını kapat.
            var update = await client.PutAsJsonAsync($"/api/settings/users/{userId}", new
            {
                pushNotificationsEnabled = true,
                emailNotificationsEnabled = true,
                upcomingLessonReminderEnabled = true,
                homeworkReminderEnabled = false,
                paymentReminderEnabled = true,
                weeklySummaryEnabled = true,
                privacyLevel = 1,
                sessionTerminationPolicy = 1
            });
            update.EnsureSuccessStatusCode();

            // Aynı türde ikinci bir bildirim: artık push GİTMEMELİ.
            pushSender.SentTokens.Clear();
            var secondNotificationId = await AddUserNotificationAsync(factory, userId, UserNotificationKind.AssignmentCreated);
            await DispatchAsync(factory);
            Assert.Empty(pushSender.SentTokens);

            // In-app kaydı duruyor (bildirim merkezinde görünür), yalnız push atlandı.
            var list = await client.GetAsync($"/api/notifications/users/{userId}/notifications?onlyUnread=false&skip=0&take=20");
            list.EnsureSuccessStatusCode();
            var payload = await list.Content.ReadFromJsonAsync<JsonElement>();
            var ids = payload.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid())
                .ToList();
            Assert.Contains(secondNotificationId, ids);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", previousConnectionString);
        }
    }

    private static async Task<Guid> AddUserNotificationAsync(
        WebApplicationFactory<Program> factory, Guid userId, UserNotificationKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var id = Guid.NewGuid();
        dbContext.UserNotifications.Add(
            new UserNotification(id, userId, kind, "Yeni odev", "Matematik odevi eklendi", "/student/assignments", DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
        return id;
    }

    private static async Task DispatchAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<INotificationDispatchProcessor>();
        await processor.DispatchPendingUserNotificationsAsync();
    }

    private sealed class RecordingPushSender : IPushSender
    {
        public List<string> SentTokens { get; } = [];

        public Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken cancellationToken = default)
        {
            SentTokens.Add(deviceToken);
            return Task.FromResult(new PushResult(PushDeliveryStatus.Delivered, null));
        }
    }

    private static async Task<(Guid UserId, string Token)> RegisterAsync(HttpClient client, string email)
    {
        var previousAuthorization = client.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.Authorization = null;
        try
        {
            var response = await client.PostAsJsonAsync("/api/identity/register", new
            {
                email,
                password = "Settings123!",
                firstName = "Ayar",
                lastName = "Kullanici",
                phoneNumber = "5551110000",
                roles = new[] { 3 }
            });
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            return (payload.GetProperty("userId").GetGuid(), payload.GetProperty("accessToken").GetString()!);
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization = previousAuthorization;
        }
    }
}
