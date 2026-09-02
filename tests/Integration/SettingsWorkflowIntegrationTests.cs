using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

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
