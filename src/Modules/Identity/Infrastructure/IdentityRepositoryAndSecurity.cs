using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EgitimUssu.Modules.Identity.Infrastructure;

internal sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserAccountRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.UserAccounts
            .Include(user => user.RoleMemberships)
            .Include(user => user.RefreshSessions)
            .Include(user => user.SecurityTokens)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public Task<UserAccount?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return _dbContext.UserAccounts
            .Include(user => user.RoleMemberships)
            .Include(user => user.RefreshSessions)
            .Include(user => user.SecurityTokens)
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public Task<UserAccount?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken)
    {
        return _dbContext.UserAccounts
            .Include(user => user.RoleMemberships)
            .Include(user => user.RefreshSessions)
            .Include(user => user.SecurityTokens)
            .FirstOrDefaultAsync(user => user.RefreshSessions.Any(session => session.RefreshTokenHash == refreshTokenHash), cancellationToken);
    }

    public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken)
    {
        return _dbContext.UserAccounts.AddAsync(userAccount, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
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
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);
        var claims = new List<Claim>
        {
            // Y4: jti — token blacklist (anlık iptal) için benzersiz token kimliği.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
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

internal sealed class NullIdentityNotificationService : IIdentityNotificationService
{
    public Task SendPasswordResetAsync(string email, string token, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SendEmailVerificationAsync(string email, string token, CancellationToken cancellationToken) => Task.CompletedTask;
}
