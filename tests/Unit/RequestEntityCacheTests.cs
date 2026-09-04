using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// O2: Authorizer'ın yüklediği varlığı handler'a taşıyan istek-kapsamlı önbellek.
/// Kapsam sızıntısı olursa bir kullanıcının verisi başka bir isteğe geçebilir — bu yüzden
/// kapsam izolasyonu davranışın parçasıdır, sadece performans detayı değildir.
/// </summary>
public sealed class RequestEntityCacheTests
{
    private sealed class Profile
    {
        public Guid Id { get; init; }
    }

    private sealed class Invoice
    {
        public Guid Id { get; init; }
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedInfrastructure(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void TryGet_Should_Return_The_Same_Instance_That_Was_Set()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IRequestEntityCache>();
        var key = Guid.NewGuid();
        var profile = new Profile { Id = key };

        cache.Set(key, profile);

        Assert.True(cache.TryGet<Profile>(key, out var found));
        Assert.Same(profile, found);
    }

    [Fact]
    public void TryGet_Should_Fail_For_Unknown_Key()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IRequestEntityCache>();
        cache.Set(Guid.NewGuid(), new Profile());

        Assert.False(cache.TryGet<Profile>(Guid.NewGuid(), out var found));
        Assert.Null(found);
    }

    [Fact]
    public void TryGet_Should_Fail_When_Another_Type_Is_Stored_Under_The_Same_Key()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IRequestEntityCache>();
        var key = Guid.NewGuid();
        cache.Set(key, new Invoice { Id = key });

        Assert.False(cache.TryGet<Profile>(key, out var found));
        Assert.Null(found);
    }

    [Fact]
    public void Cache_Should_Not_Leak_Between_Request_Scopes()
    {
        using var provider = BuildProvider();
        var key = Guid.NewGuid();

        using (var first = provider.CreateScope())
        {
            first.ServiceProvider.GetRequiredService<IRequestEntityCache>().Set(key, new Profile { Id = key });
        }

        using var second = provider.CreateScope();
        var cache = second.ServiceProvider.GetRequiredService<IRequestEntityCache>();

        Assert.False(cache.TryGet<Profile>(key, out var leaked));
        Assert.Null(leaked);
    }

    [Fact]
    public void Same_Scope_Should_Resolve_The_Same_Cache_Instance()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<IRequestEntityCache>();
        var second = scope.ServiceProvider.GetRequiredService<IRequestEntityCache>();

        Assert.Same(first, second);
    }
}

/// <summary>
/// O2: Authorizer ve handler aynı varlığı <c>GetOrLoadAsync</c> ile ister; hangisi önce koşarsa
/// yükler, diğeri istek kapsamından okur.
/// </summary>
public sealed class RequestEntityCacheGetOrLoadTests
{
    private sealed class Profile
    {
        public Guid Id { get; init; }
    }

    [Fact]
    public async Task Second_Call_Should_Not_Invoke_The_Loader()
    {
        var cache = new EgitimUssu.Tests.Unit.TestDoubles.FakeRequestEntityCache();
        var key = Guid.NewGuid();
        var loads = 0;
        Task<Profile?> Load(CancellationToken _)
        {
            loads++;
            return Task.FromResult<Profile?>(new Profile { Id = key });
        }

        var first = await cache.GetOrLoadAsync(key, Load, CancellationToken.None);
        var second = await cache.GetOrLoadAsync(key, Load, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Missing_Entity_Should_Not_Be_Cached()
    {
        var cache = new EgitimUssu.Tests.Unit.TestDoubles.FakeRequestEntityCache();
        var key = Guid.NewGuid();
        var loads = 0;
        Task<Profile?> Load(CancellationToken _)
        {
            loads++;
            return Task.FromResult<Profile?>(null);
        }

        Assert.Null(await cache.GetOrLoadAsync(key, Load, CancellationToken.None));
        Assert.Null(await cache.GetOrLoadAsync(key, Load, CancellationToken.None));
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task Different_Keys_Should_Load_Separately()
    {
        var cache = new EgitimUssu.Tests.Unit.TestDoubles.FakeRequestEntityCache();
        var loads = 0;
        Task<Profile?> Load(CancellationToken _)
        {
            loads++;
            return Task.FromResult<Profile?>(new Profile());
        }

        await cache.GetOrLoadAsync(("byId", Guid.NewGuid()), Load, CancellationToken.None);
        await cache.GetOrLoadAsync(("byUserId", Guid.NewGuid()), Load, CancellationToken.None);

        Assert.Equal(2, loads);
    }
}
