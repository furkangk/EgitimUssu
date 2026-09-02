using EgitimUssu.Modules.Settings.Domain;
using EgitimUssu.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Settings.Infrastructure;

/// <summary>
/// M15-3: Settings'in oturum politikası sözleşmesini uygulaması. Identity bu arayüz üzerinden okur;
/// Settings DbContext'ine doğrudan erişmez (modül sınırı korunur).
/// </summary>
internal sealed class SessionPolicyDirectory : ISessionPolicyDirectory
{
    private readonly SettingsDbContext _dbContext;

    public SessionPolicyDirectory(SettingsDbContext dbContext) => _dbContext = dbContext;

    public async Task<bool> ShouldTerminateOtherSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var policy = await _dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => (SessionTerminationPolicy?)s.SessionTerminationPolicy)
            .FirstOrDefaultAsync(cancellationToken);

        // Kayıt yoksa varsayılan KeepLatest → diğer oturumlar korunur.
        return policy == SessionTerminationPolicy.TerminateOtherSessions;
    }
}
