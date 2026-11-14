using System.Net;
using System.Net.Http.Json;
using EgitimUssu.Modules.Identity.Infrastructure;
using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// N-41 (P15 Task 8): Arka plan işi kirası <b>sahibince yenilenir</b>.
/// <para>
/// Kira yalnız "süresi dolmuşsa" devralınabiliyordu; sahip kontrolü yoktu. Kira süresi en az beş
/// dakika olduğu için aynı örnek bir sonraki turda <b>kendi</b> kirasına takılıyor, tur atlanıyordu:
/// 15 sn'lik e-posta kuyruğu ~5 dk'da bir boşalıyordu (şifre sıfırlama e-postası dahil), 30 sn'lik
/// push gönderimi 5 dk'da bir, saatlik tarayıcılar 2 saatte bir, günlük anlık görüntü 48 saatte bir.
/// </para>
/// <para>
/// A-07'nin ikinci yarısı: <c>AccountDeletionService</c> ve <c>DataRetentionService</c> düz
/// <see cref="BackgroundService"/> idi — ikinci bir uygulama örneği aynı silme/temizlik turunu
/// eşzamanlı koşabiliyordu.
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealJobLeaseRenewalTests(ContainerFixture fixture)
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly DateTime Start = new(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Gerçek Postgres: koşullu <c>ON CONFLICT DO UPDATE ... WHERE</c> dalı.</summary>
    [SkippableFact]
    public async Task Owner_Should_Renew_Its_Own_Lease_And_Others_Should_Wait_For_Expiry_On_Postgres()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var envScope = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        await AssertRenewalRulesAsync(factory);
    }

    /// <summary>
    /// InMemory dalı (geliştirme ve InMemory integration testleri): aynı kural orada da geçerli olmalı,
    /// yoksa yerelde ve testlerde arka plan işleri yine 5 dk'da bir koşar.
    /// </summary>
    [Fact]
    public async Task Owner_Should_Renew_Its_Own_Lease_And_Others_Should_Wait_For_Expiry_In_Memory()
    {
        using var envScope = UseInMemory($"job-lease-renewal-{Guid.NewGuid():N}");
        await using var factory = new WebApplicationFactory<Program>();

        await AssertRenewalRulesAsync(factory);
    }

    /// <summary>
    /// Raporun senaryosu: örnek e-posta kirasını elinde tutarken kuyruğa giren e-posta bir sonraki
    /// turda gitmeli. Düzeltmeden önce kira beş dakika boyunca sahibini de dışarıda bırakıyordu.
    /// </summary>
    [SkippableFact]
    public async Task Queued_Email_Should_Be_Sent_Within_15_Seconds_While_This_Instance_Holds_The_Lease()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var envScope = RealInfrastructure.Use(fixture);
        var previousPollInterval = Environment.GetEnvironmentVariable("Outbox__PollIntervalSeconds");
        Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", "1");

        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.41.0.1");

            // E-posta kirası bu örneğin elinde olsun. Dönüş değeri önemsiz: ilk tur ya da aynı süreçteki
            // önceki bir host kirayı zaten almış olabilir (hepsi aynı InstanceId'yi paylaşır) — her
            // durumda kayıttan önce kira satırı vardır ve sahibi bu süreçtir. Böylece e-posta, "ilk
            // tur kirayı yeni aldı" şansıyla değil, ancak sahibin kirayı yenilemesiyle gidebilir.
            await HoldEmailDispatchLeaseAsync(factory);

            var email = $"n41-{Guid.NewGuid():N}@example.com";
            var register = await client.PostAsJsonAsync("/api/identity/register", new
            {
                email,
                password = "Sifre12345!",
                firstName = "Ada",
                lastName = "Yilmaz",
                phoneNumber = (string?)null,
                roles = new[] { 2 },
            });
            Assert.Equal(HttpStatusCode.OK, register.StatusCode);

            Assert.True(
                await WaitForSentEmailAsync(factory, email, TimeSpan.FromSeconds(15)),
                "Doğrulama e-postası 15 sn içinde gönderilmedi: örnek kendi kirasını yenileyemiyor (N-41).");
        }
        finally
        {
            Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", previousPollInterval);
        }
    }

    /// <summary>
    /// A-07: Modüllerin her zamanlayıcılı arka plan işi kira altında koşar. Tek istisna outbox
    /// dağıtıcısıdır — o kira yerine satır sahiplenmesiyle (<c>FOR UPDATE SKIP LOCKED</c>) korunur.
    /// </summary>
    [Fact]
    public async Task Every_Scheduled_Background_Job_Should_Run_Under_A_Lease()
    {
        using var envScope = UseInMemory($"job-lease-coverage-{Guid.NewGuid():N}");
        await using var factory = new WebApplicationFactory<Program>();

        var jobs = factory.Services.GetServices<IHostedService>()
            .Where(service => service.GetType().Assembly.GetName().Name!.StartsWith("EgitimUssu", StringComparison.Ordinal))
            .ToArray();

        var unleased = jobs
            .Where(service => service is not LeasedBackgroundService and not OutboxDispatcher)
            .Select(service => service.GetType().Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(jobs);
        Assert.Empty(unleased);
    }

    private static async Task AssertRenewalRulesAsync(WebApplicationFactory<Program> factory)
    {
        var jobName = $"renewal-{Guid.NewGuid():N}";
        var clock = new MutableClock(Start);
        var ownerA = new JobLeaseStore(clock, Guid.NewGuid());
        var ownerB = new JobLeaseStore(clock, Guid.NewGuid());

        // Sahip 15 sn aralıkla üç tur koşar: her turda kendi kirasını yeniler.
        Assert.True(await TryAcquireAsync(factory, ownerA, jobName));
        clock.UtcNow = Start.AddSeconds(15);
        Assert.True(await TryAcquireAsync(factory, ownerA, jobName));
        clock.UtcNow = Start.AddSeconds(30);
        Assert.True(await TryAcquireAsync(factory, ownerA, jobName));

        // Başka bir örnek, kira dolmadan alamaz.
        clock.UtcNow = Start.AddSeconds(45);
        Assert.False(await TryAcquireAsync(factory, ownerB, jobName));

        // Yenileme süreyi gerçekten uzatır: ilk alışın süresi (09:05:00) geçti ama son yenilemeninki
        // (09:05:30) geçmedi — kira hâlâ sahibinde.
        clock.UtcNow = Start.Add(Lease).AddSeconds(10);
        Assert.False(await TryAcquireAsync(factory, ownerB, jobName));

        // Sahip çöktü (yenilemedi): kira dolunca diğer örnek devralır, eski sahip geri dönse de alamaz.
        clock.UtcNow = Start.AddSeconds(30).Add(Lease).AddSeconds(1);
        Assert.True(await TryAcquireAsync(factory, ownerB, jobName));
        Assert.False(await TryAcquireAsync(factory, ownerA, jobName));
    }

    private static async Task<bool> TryAcquireAsync(
        WebApplicationFactory<Program> factory,
        JobLeaseStore store,
        string jobName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        return await store.TryAcquireAsync(dbContext, jobName, Lease);
    }

    /// <summary>
    /// E-posta dağıtıcısının kirasını DI'daki depoyla (bu sürecin sahibi adına) alır; kira,
    /// dağıtıcının kullandığı şemada — adı alfabetik ilk modülde — tutulur.
    /// </summary>
    private static async Task HoldEmailDispatchLeaseAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var descriptor = scope.ServiceProvider.GetServices<ModuleDbContextDescriptor>()
            .OrderBy(item => item.ModuleName, StringComparer.Ordinal)
            .First();
        var dbContext = (ModuleDbContext)scope.ServiceProvider.GetRequiredService(descriptor.DbContextType);
        var leases = scope.ServiceProvider.GetRequiredService<IJobLeaseStore>();

        await leases.TryAcquireAsync(dbContext, "outgoing-email-dispatch", Lease);

        var lease = await dbContext.JobLeases.AsNoTracking()
            .SingleAsync(item => item.JobName == "outgoing-email-dispatch");
        Assert.Equal(JobLeaseStore.InstanceId, lease.OwnerId);
        Assert.True(lease.ExpiresOnUtc > DateTime.UtcNow);
    }

    private static async Task<bool> WaitForSentEmailAsync(
        WebApplicationFactory<Program> factory,
        string email,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            if (await dbContext.OutgoingEmails.AsNoTracking()
                    .AnyAsync(item => item.ToEmail == email && item.SentOnUtc != null))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return false;
    }

    private static IDisposable UseInMemory(string databaseName)
        => new EnvironmentVariable("ConnectionStrings__Postgres", "InMemory:" + databaseName);

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class EnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariable(string name, string value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
