using EgitimUssu.Shared.Infrastructure.Configuration;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P13 Task 4 / E-05: Açılışta migration uygulanmalı mı?
/// <para>
/// Kural: <b>üretimde asla</b>. Şema değişikliği geri alınması en pahalı işlemdir; uygulamanın
/// açılış anına bağlanırsa iki şey birden olur — değişiklik <i>ne zaman</i> uygulandığı
/// belirsizleşir (deploy ile aynı saniyede), ve birden çok örnek aynı anda açıldığında migration
/// yarışır. Üretimde migration ayrı ve <b>gözetilen</b> bir deploy adımıdır
/// (<c>dotnet ef database update</c>).
/// </para>
/// <para>
/// Guard saf bir karar fonksiyonudur: ortam adı + bayrak → engel nedeni (ya da <c>null</c>).
/// Böylece "üretimde asla" kuralı, host ayağa kaldırmadan tablo halinde kanıtlanabilir —
/// <c>DemoDataSeedGuard</c> ile aynı desen (P13 Task 3).
/// </para>
/// </summary>
public sealed class MigrationStartupGuardTests
{
    [Theory]
    [InlineData("Production", true, false)]   // bayrak açık OLSA BİLE üretimde koşmaz
    [InlineData("Production", false, false)]
    [InlineData("Staging", true, true)]
    [InlineData("Development", true, true)]
    [InlineData("Staging", false, false)]
    [InlineData("Development", false, false)]
    public void Guard_Should_Block_Production_And_Disabled_Environments(
        string environmentName, bool enabled, bool expectedAllowed)
    {
        var reason = MigrationStartupGuard.BlockReason(environmentName, enabled);

        Assert.Equal(expectedAllowed, reason is null);
    }

    /// <summary>Ortam adı büyük/küçük harf yazımına takılmamalı.</summary>
    [Theory]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    public void Guard_Should_Match_Production_Case_Insensitively(string environmentName)
    {
        Assert.NotNull(MigrationStartupGuard.BlockReason(environmentName, enabled: true));
    }

    /// <summary>
    /// Üretimde engellenen bir açılış, operatöre <b>ne yapması gerektiğini</b> söylemelidir.
    /// Yalnız "atlandı" diyen bir uyarı, şeması eksik bir veritabanıyla açılan uygulamanın
    /// önündeki tek ipucudur; alternatifi yazmayan uyarı sessiz başarısızlıktan az farklıdır.
    /// </summary>
    [Fact]
    public void Production_Block_Reason_Should_Name_The_Separate_Deploy_Step()
    {
        var reason = MigrationStartupGuard.BlockReason("Production", enabled: true);

        Assert.NotNull(reason);
        Assert.Contains("dotnet ef database update", reason, StringComparison.Ordinal);
    }
}
