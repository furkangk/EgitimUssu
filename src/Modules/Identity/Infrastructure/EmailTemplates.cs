using EgitimUssu.Shared.Infrastructure.Email;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// Kimlik akışlarının e-posta şablonları. Basit string şablon: harici motor bağımlılığı yok,
/// içerik testlerle sabitlenir.
/// </summary>
internal static class EmailTemplates
{
    private const string BrandName = "EğitimÜssü";

    public static EmailMessage PasswordReset(string toEmail, string token, string appLinkScheme)
    {
        // Token base64'tur; "+" ve "/" sorgu dizesinde kodlanmazsa mobil taraf yanlis token okur.
        var link = $"{appLinkScheme}://password-reset?token={Uri.EscapeDataString(token)}";
        var html = $"""
            <div style="font-family:system-ui,Segoe UI,Roboto,sans-serif;color:#0f172a">
              <h2 style="color:#082B4F">{BrandName} — Şifre sıfırlama</h2>
              <p>Şifreni sıfırlamak için aşağıdaki bağlantıya dokun:</p>
              <p><a href="{link}" style="background:#082B4F;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none">Şifremi sıfırla</a></p>
              <p>Bağlantı açılmazsa uygulamadaki "Şifremi unuttum" ekranına şu kodu yapıştır:</p>
              <p style="font-size:18px;font-weight:700;letter-spacing:1px">{token}</p>
              <p style="color:#64748b;font-size:13px">Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin. Kod 1 saat geçerlidir.</p>
            </div>
            """;
        var text = $"""
            {BrandName} — Şifre sıfırlama

            Kodun: {token}
            Bağlantı: {link}

            Bu isteği sen yapmadıysan yok sayabilirsin. Kod 1 saat geçerlidir.
            """;

        return new EmailMessage(toEmail, toEmail, $"{BrandName} — Şifre sıfırlama", html, text);
    }

    public static EmailMessage EmailVerification(string toEmail, string token, string appLinkScheme)
    {
        // Token base64'tur; "+" ve "/" sorgu dizesinde kodlanmazsa mobil taraf yanlis token okur.
        var link = $"{appLinkScheme}://verify-email?token={Uri.EscapeDataString(token)}";
        var html = $"""
            <div style="font-family:system-ui,Segoe UI,Roboto,sans-serif;color:#0f172a">
              <h2 style="color:#082B4F">{BrandName} — E-posta doğrulama</h2>
              <p>Hesabını etkinleştirmek için aşağıdaki bağlantıya dokun:</p>
              <p><a href="{link}" style="background:#082B4F;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none">E-postamı doğrula</a></p>
              <p>Bağlantı açılmazsa uygulamadaki doğrulama ekranına şu kodu yapıştır:</p>
              <p style="font-size:18px;font-weight:700;letter-spacing:1px">{token}</p>
              <p style="color:#64748b;font-size:13px">Kod 24 saat geçerlidir.</p>
            </div>
            """;
        var text = $"""
            {BrandName} — E-posta doğrulama

            Kodun: {token}
            Bağlantı: {link}

            Kod 24 saat geçerlidir.
            """;

        return new EmailMessage(toEmail, toEmail, $"{BrandName} — E-posta doğrulama", html, text);
    }
}
