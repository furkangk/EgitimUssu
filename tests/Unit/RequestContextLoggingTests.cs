using EgitimUssu.Shared.Infrastructure.Observability;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P13 Task 1 / C-04: Log korelasyonu ve PII maskesi.
/// <para>
/// Kritik kural — <b>PII log'a yazılmaz</b>: e-posta, telefon, token ve parola sorgu dizesinde
/// gelse bile log satırına düz metin olarak girmez. Log'lar toplayıcıya gider ve orada
/// saklanır; oraya sızan bir token, tek bir log satırıyla kalıcı bir güvenlik açığıdır.
/// </para>
/// </summary>
public sealed class RequestContextLoggingTests
{
    [Theory]
    [InlineData("/api/identity/verify?token=abc123", "/api/identity/verify?token=***")]
    [InlineData("/api/identity/reset?email=ali@example.com", "/api/identity/reset?email=***")]
    [InlineData("/api/users?phone=05551112233", "/api/users?phone=***")]
    [InlineData("/api/x?password=hunter2", "/api/x?password=***")]
    public void Sensitive_Query_Values_Should_Be_Masked(string input, string expected)
    {
        Assert.Equal(expected, SensitiveDataMask.MaskQueryString(input));
    }

    /// <summary>Maskeleme büyük/küçük harfe ve alan adının ekine takılmamalı.</summary>
    [Theory]
    [InlineData("/a?Token=x", "/a?Token=***")]
    [InlineData("/a?accessToken=x", "/a?accessToken=***")]
    [InlineData("/a?refresh_token=x", "/a?refresh_token=***")]
    [InlineData("/a?userEmail=x", "/a?userEmail=***")]
    public void Masking_Should_Be_Case_And_Suffix_Insensitive(string input, string expected)
    {
        Assert.Equal(expected, SensitiveDataMask.MaskQueryString(input));
    }

    [Fact]
    public void Multiple_Parameters_Should_Mask_Only_Sensitive_Ones()
    {
        var masked = SensitiveDataMask.MaskQueryString("/api/x?page=2&token=abc&sort=name");

        Assert.Equal("/api/x?page=2&token=***&sort=name", masked);
    }

    [Fact]
    public void Path_Without_Query_Should_Be_Unchanged()
    {
        Assert.Equal("/api/students/profiles", SensitiveDataMask.MaskQueryString("/api/students/profiles"));
    }

    /// <summary>Değeri olmayan parametre bozulmadan geçer (log satırı okunabilir kalsın).</summary>
    [Fact]
    public void Valueless_Parameter_Should_Survive()
    {
        Assert.Equal("/api/x?flag&token=***", SensitiveDataMask.MaskQueryString("/api/x?flag&token=abc"));
    }
}
