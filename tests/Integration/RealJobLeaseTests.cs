using EgitimUssu.Modules.Notifications.Infrastructure;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Integration;

/// <summary>
/// A-07: Arka plan işi kirası gerçek Postgres'e karşı.
/// <para>
/// Modül arka plan servisleri (bildirim gönderimi, ödeme/ödev taramaları, ilerleme anlık görüntüsü)
/// düz bir zamanlayıcı döngüsüydü; kilit ya da satır sahiplenme yoktu. İkinci bir uygulama örneği
/// açıldığı anda aynı <c>Pending</c> satırları iki kez okunuyor ve <b>çift push bildirimi</b>
/// üretiliyordu — her örnek farklı bir <c>EventId</c> ürettiği için tüketici tarafındaki inbox
/// dedup'ı da bunu yakalayamıyordu.
/// </para>
/// <para>
/// Kiranın doğruluğu <c>ON CONFLICT DO UPDATE ... WHERE</c> ifadesinin atomikliğine dayanır;
/// bunu yalnız gerçek Postgres doğrulayabilir (InMemory sağlayıcıda böyle bir yarış yoktur).
/// </para>
/// </summary>
[Collection("containers")]
public sealed class RealJobLeaseTests(ContainerFixture fixture)
{
    [SkippableFact]
    public async Task Only_One_Instance_Can_Hold_A_Lease_At_A_Time()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var envScope = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var jobName = $"test-job-{Guid.NewGuid():N}";

        // N-41: Aynı süreçteki her host aynı InstanceId'yi paylaşır ve sahip kendi kirasını yenileyebilir;
        // "iki örnek" ancak iki ayrı sahip kimliğiyle kurulur. Eski hâli tek sahiple ikinci denemenin
        // false dönmesini bekliyordu — yani sahibin kendi kirasına takılmasını (bozuk davranış) kilitliyordu.
        var first = await TryAcquireAsync(factory, NewInstance(), jobName, TimeSpan.FromMinutes(10));
        var second = await TryAcquireAsync(factory, NewInstance(), jobName, TimeSpan.FromMinutes(10));

        Assert.True(first);
        Assert.False(second);
    }

    /// <summary>
    /// Çöken bir örnek işi kalıcı olarak kilitlememeli: kira süresi dolduğunda devralınabilir.
    /// </summary>
    [SkippableFact]
    public async Task Expired_Lease_Can_Be_Taken_Over()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var envScope = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();
        var jobName = $"test-job-{Guid.NewGuid():N}";

        // Hemen sona eren bir kira: sahibi çökmüş bir örneği temsil eder. Devralan başka bir örnek
        // olmalı — aynı sahip, süre dolmasa da yenileyebildiği için bunu kanıtlamaz.
        Assert.True(await TryAcquireAsync(factory, NewInstance(), jobName, TimeSpan.Zero));

        Assert.True(await TryAcquireAsync(factory, NewInstance(), jobName, TimeSpan.FromMinutes(10)));
    }

    [SkippableFact]
    public async Task Different_Jobs_Do_Not_Block_Each_Other()
    {
        Skip.IfNot(fixture.Available, "Docker gerekli (Testcontainers).");

        using var envScope = RealInfrastructure.Use(fixture);
        await using var factory = new WebApplicationFactory<Program>();

        Assert.True(await TryAcquireAsync(factory, NewInstance(), $"job-a-{Guid.NewGuid():N}", TimeSpan.FromMinutes(10)));
        Assert.True(await TryAcquireAsync(factory, NewInstance(), $"job-b-{Guid.NewGuid():N}", TimeSpan.FromMinutes(10)));
    }

    /// <summary>Ayrı bir uygulama örneğini temsil eden, kendi sahip kimliğine sahip kira deposu.</summary>
    private static JobLeaseStore NewInstance() => new(new SystemClock(), Guid.NewGuid());

    private static async Task<bool> TryAcquireAsync(
        WebApplicationFactory<Program> factory,
        IJobLeaseStore store,
        string jobName,
        TimeSpan leaseDuration)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        return await store.TryAcquireAsync(dbContext, jobName, leaseDuration);
    }
}
