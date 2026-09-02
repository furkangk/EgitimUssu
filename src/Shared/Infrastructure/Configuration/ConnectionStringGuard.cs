namespace EgitimUssu.Shared.Infrastructure.Configuration;

/// <summary>
/// A-06: Postgres bağlantı dizesini doğrular (<see cref="JwtSigningKeyGuard"/> ile aynı desen).
/// Sır repoda tutulmaz: dize <c>ConnectionStrings__Postgres</c> ortam değişkeninden gelir.
/// Üretimde boş ya da varsayılan/zayıf parolalı bir dizeyle uygulama açılmaz.
/// </summary>
public static class ConnectionStringGuard
{
    /// <summary>Testlerde ve geliştirmede kullanılan bellek-içi sağlayıcı öneki; parola denetiminden muaftır.</summary>
    private const string InMemoryPrefix = "InMemory:";

    // Üretimde kabul edilmeyen, örneklerde/varsayılanlarda geçen parolalar (boş parola dahil).
    private static readonly HashSet<string> WeakPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "postgres",
        "password",
        "changeme",
        "change-me",
        "admin",
        "root",
        "secret",
        "123456"
    };

    /// <summary>Dize geçersizse nedenini döndürür; geçerliyse <c>null</c>.</summary>
    public static string? Validate(string? connectionString, bool isDevelopment)
    {
        if (connectionString is not null
            && connectionString.StartsWith(InMemoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "ConnectionStrings:Postgres boş. Bağlantı dizesini ConnectionStrings__Postgres ortam değişkeniyle verin.";
        }

        if (isDevelopment)
        {
            return null;
        }

        var password = ReadPassword(connectionString);
        if (password is not null && WeakPasswords.Contains(password))
        {
            return "ConnectionStrings:Postgres varsayılan/zayıf parola içeriyor. Üretimde gerçek bir sır kullanın.";
        }

        return null;
    }

    /// <summary>Dize geçersizse startup'ı fail-fast durdurur.</summary>
    public static void EnsureValid(string? connectionString, bool isDevelopment)
    {
        var error = Validate(connectionString, isDevelopment);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <summary>
    /// Bağlantı dizesindeki parola değerini okur. Anahtar adı yoksa <c>null</c> döner
    /// (parolasız kimlik doğrulama — ör. Npgsql integrated security — bilinçli olarak engellenmez).
    /// </summary>
    private static string? ReadPassword(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = part[..separatorIndex].Trim();
            if (key.Equals("Password", StringComparison.OrdinalIgnoreCase)
                || key.Equals("pwd", StringComparison.OrdinalIgnoreCase))
            {
                return part[(separatorIndex + 1)..].Trim();
            }
        }

        return null;
    }
}
