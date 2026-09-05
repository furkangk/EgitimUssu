using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Kernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>
/// A-08: Kullanıcı hesabı deposu — çocuk koleksiyonlar <b>filtreli</b> yüklenir.
/// <para>
/// Eskiden üç metot da <c>RefreshSessions</c> ve <c>SecurityTokens</c> koleksiyonlarının
/// <b>tamamını</b> çekiyordu. Oturumlar 30 gün ömürlü ve hiç silinmiyor; her giriş bir satır daha
/// ekliyor, eskisini <c>Revoke</c> edip bırakıyordu. Sonuç: her giriş ve her token yenilemesi
/// kullanıcının bütün geçmişini belleğe çekiyor, üstelik üç <c>Include</c> aynı sorguda kartezyen
/// büyüme üretiyordu — günde bir giren bir kullanıcı bir yıl sonra her istekte yüzlerce satır taşır.
/// </para>
/// <para>
/// Artık yalnız <b>işe yarayan</b> satırlar yüklenir: etkin oturumlar ve kullanılabilir güvenlik
/// token'ları. Kullanılmış/süresi geçmiş satırları hiçbir handler okumaz.
/// </para>
/// </summary>
internal sealed class UserAccountRepository : IUserAccountRepository
{
    /// <summary>Süresi bu kadar süre önce dolmuş satırlar artık hiçbir akışta gerekmez.</summary>
    private static readonly TimeSpan PurgeGrace = TimeSpan.FromDays(30);

    private readonly IdentityDbContext _dbContext;
    private readonly IClock _clock;

    public UserAccountRepository(IdentityDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        return _dbContext.UserAccounts
            .Include(user => user.RoleMemberships)
            .Include(user => user.RefreshSessions.Where(session => session.RevokedOnUtc == null && session.ExpiresOnUtc > now))
            .Include(user => user.SecurityTokens.Where(token => token.UsedOnUtc == null && token.ExpiresOnUtc > now))
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        return _dbContext.UserAccounts
            .Include(user => user.RoleMemberships)
            .Include(user => user.RefreshSessions.Where(session => session.RevokedOnUtc == null && session.ExpiresOnUtc > now))
            .Include(user => user.SecurityTokens.Where(token => token.UsedOnUtc == null && token.ExpiresOnUtc > now))
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public Task<UserAccount?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        return _dbContext.UserAccounts
            .Include(user => user.RoleMemberships)
            // A-15: Aranan oturum, iptal edilmiş olsa bile yüklenir — "iptal edilmiş bir refresh
            // token'ın tekrar sunulması" ele geçirme sinyalidir ve ancak satır elde varsa görülür.
            // Yanına etkin oturumlar da alınır; ihlal tespitinde hepsi birden iptal edilecek.
            .Include(user => user.RefreshSessions.Where(session =>
                session.RefreshTokenHash == refreshTokenHash
                || (session.RevokedOnUtc == null && session.ExpiresOnUtc > now)))
            .FirstOrDefaultAsync(
                user => user.RefreshSessions.Any(session => session.RefreshTokenHash == refreshTokenHash),
                cancellationToken);
    }

    public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken)
    {
        return _dbContext.UserAccounts.AddAsync(userAccount, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A-08: Kullanıcının çoktan işlevsiz kalmış oturum ve güvenlik token'ı satırlarını siler.
    /// Filtreli <c>Include</c>'lar okumayı sınırlar ama tablo yine tek yönde büyürdü; budama
    /// girişte, yalnız o kullanıcının satırları için ve tek DELETE ile yapılır.
    /// </summary>
    public async Task PurgeStaleCredentialsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // `ExecuteDelete` ilişkisel olmayan sağlayıcıda (test/dev InMemory) desteklenmez.
        if (!_dbContext.Database.IsRelational())
        {
            return;
        }

        var cutoff = _clock.UtcNow - PurgeGrace;

        await _dbContext.RefreshTokenSessions
            .Where(session => session.UserAccountId == userId && session.ExpiresOnUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.UserSecurityTokens
            .Where(token => token.UserAccountId == userId && token.ExpiresOnUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}

internal sealed class AspNetPasswordHasher : Application.IPasswordHasher
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public string Hash(string password)
    {
        return _passwordHasher.HashPassword(new object(), password);
    }

    public bool Verify(string hashedPassword, string providedPassword)
    {
        var verificationResult = _passwordHasher.VerifyHashedPassword(new object(), hashedPassword, providedPassword);
        return verificationResult is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}

internal sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly JwtOptions _jwtOptions;

    public JwtTokenIssuer(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public (string AccessToken, DateTime ExpiresAtUtc) Issue(UserAccount userAccount)
    {
        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(_jwtOptions.ExpiryMinutes);
        var claims = new List<Claim>
        {
            // Y4: jti — token blacklist (anlık iptal) için benzersiz token kimliği.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            // A-06: iat — kullanıcı bazlı toplu iptal ("şu andan önce verilenler geçersiz") bu
            // damgaya dayanır. Parola sıfırlama ve rol değişimi tek tek jti'leri bilemez.
            new(
                JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAtUtc, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Sub, userAccount.Id.ToString()),
            new(ClaimTypes.NameIdentifier, userAccount.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, userAccount.Email),
            new(ClaimTypes.Email, userAccount.Email),
            new(JwtRegisteredClaimNames.GivenName, userAccount.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, userAccount.LastName)
        };

        claims.AddRange(userAccount.RoleMemberships.Select(role => new Claim(ClaimTypes.Role, role.Role.ToString())));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}

internal sealed class Sha256TokenProtector : ITokenProtector
{
    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes);
    }
}
