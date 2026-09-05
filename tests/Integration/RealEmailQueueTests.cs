using System.Net;
using System.Net.Http.Json;
using EgitimUssu.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// A-04: Kimlik e-postaları istek yolunda gönderilmez; iş yazımıyla <b>aynı transaction'da</b>
/// kuyruğa girer.
/// <para>
/// Eski akış <c>SaveChanges</c>'ten sonra doğrudan SMTP'ye gidiyor ve hatayı yeniden fırlatıyordu:
/// sağlayıcı yavaş ya da kapalıysa kullanıcı <b>oluşmuş ve commit edilmiş</b> olmasına rağmen
/// istek 500 dönüyor, kullanıcı tekrar denediğinde <c>duplicate_email</c> (409) alıyordu — çıkışı
/// olmayan bir durum.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealEmailQueueTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Registration_Should_Queue_The_Verification_Email_Instead_Of_Sending_Inline()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var envScope = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.40.0.1");

        var email = $"a04-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/identity/register", new
        {
            email,
            password = "Sifre12345!",
            firstName = "Ada",
            lastName = "Yilmaz",
            phoneNumber = (string?)null,
            roles = new[] { 2 },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var queued = await dbContext.OutgoingEmails
            .AsNoTracking()
            .Where(item => item.ToEmail == email)
            .ToListAsync();

        var mail = Assert.Single(queued);
        // Kuyrukta bekliyor: gönderim arka planda, retry/backoff/dead-letter ile yapılır.
        Assert.Null(mail.SentOnUtc);
        Assert.Null(mail.DeadLetteredOnUtc);
        Assert.Equal(0, mail.RetryCount);
        Assert.Contains("EğitimÜssü", mail.Subject, StringComparison.Ordinal);
    }
}
