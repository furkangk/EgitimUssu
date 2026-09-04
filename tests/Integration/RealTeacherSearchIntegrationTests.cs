using System.Globalization;
using EgitimUssu.Modules.Teachers.Application;
using EgitimUssu.Modules.Teachers.Domain;
using EgitimUssu.Modules.Teachers.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// P06 Task 10 / M02-3: Arama filtresi <b>gerçek Postgres'te</b>, <b>Türkçe kültür altında</b>.
/// <c>string.ToLower()</c> istemci tarafında kültüre bağlıdır: tr-TR'de <c>'I' → 'ı'</c>. SQL tarafı ise
/// Postgres'in <c>lower()</c> fonksiyonudur ve <c>'I' → 'i'</c> üretir. İki taraf ayrıştığı için
/// "Istanbul" araması hiçbir sonuç döndürmüyordu. Birim testi bunu göremez: bellek içi listede
/// karşılaştırmanın **iki tarafı da** aynı .NET kültürünü kullanır.
/// </summary>
[Collection("containers")]
public sealed class RealTeacherSearchIntegrationTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task City_Filter_Should_Match_Under_Turkish_Culture_On_Real_Postgres()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var _ = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        var userId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeachersDbContext>();
        dbContext.TeacherProfiles.Add(new TeacherProfile(
            Guid.NewGuid(), userId, "Ogretmen Test", "Matematik", "Istanbul", "Kadikoy",
            null, null, TeacherLessonFormat.Hybrid, 8, "Lisans", 1200m, "TRY", false, null, DateTime.UtcNow));
        await dbContext.SaveChangesAsync();

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            // Kullanıcının yazabileceği her varyant aynı kaydı bulmalı:
            // ASCII 'I', Türkçe noktalı 'İ', tümü büyük ve tümü küçük.
            foreach (var typed in new[] { "Istanbul", "İstanbul", "ISTANBUL", "istanbul", " Istanbul " })
            {
                var query = new SearchTeacherProfilesQuery(
                    City: typed, District: null, Subject: null, LessonFormat: null,
                    MinRate: null, MaxRate: null, OnlyVerified: null, Skip: 0, Take: 10);

                var matches = await TeacherSearchFilter
                    .Apply(dbContext.TeacherProfiles.AsNoTracking(), query)
                    .CountAsync();

                Assert.True(matches == 1, $"'{typed}' araması {matches} sonuç döndürdü.");
            }

            // Türkçe 'İ' ile **kaydedilmiş** şehir de bulunmalı (Postgres lower('İ') = 'i').
            dbContext.TeacherProfiles.Add(new TeacherProfile(
                Guid.NewGuid(), Guid.NewGuid(), "Ikinci Ogretmen", "Fizik", "İzmir", "Karsiyaka",
                null, null, TeacherLessonFormat.Hybrid, 3, "Lisans", 900m, "TRY", false, null, DateTime.UtcNow));
            await dbContext.SaveChangesAsync();

            foreach (var typed in new[] { "Izmir", "İzmir", "IZMIR", "izmir" })
            {
                var query = new SearchTeacherProfilesQuery(
                    City: typed, District: null, Subject: null, LessonFormat: null,
                    MinRate: null, MaxRate: null, OnlyVerified: null, Skip: 0, Take: 10);

                var matches = await TeacherSearchFilter
                    .Apply(dbContext.TeacherProfiles.AsNoTracking(), query)
                    .CountAsync();

                Assert.True(matches == 1, $"'{typed}' araması {matches} sonuç döndürdü.");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
