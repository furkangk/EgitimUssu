using EgitimUssu.Modules.Identity.Application;
using EgitimUssu.Modules.Identity.Domain;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EgitimUssu.Modules.Identity.Infrastructure;

/// <summary>E-07: Bir hesap silme turunun sonucu.</summary>
/// <param name="CompletedCount">Bu turda tamamen anonimleştirilen hesap sayısı.</param>
/// <param name="FailedCount">Bir modül hata verdiği için <b>yarıda kesilen</b> talep sayısı.</param>
/// <param name="AnonymizedRecordCount">Tüm modüllerde etkilenen toplam kayıt sayısı.</param>
public sealed record AccountDeletionRunSummary(int CompletedCount, int FailedCount, int AnonymizedRecordCount);

/// <summary>E-07: Süresi dolan hesap silme taleplerini uygulayan tur (arka plan servisinden ayrık, test edilebilir).</summary>
public interface IAccountDeletionProcessor
{
    /// <summary>Bir tur koşturur.</summary>
    /// <param name="cancellationToken">İptal belirteci.</param>
    Task<AccountDeletionRunSummary> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// E-07: KVKK hesap silme akışının orkestratörü.
/// <para>
/// Sıra bilinçlidir: <b>önce diğer modüller, en son Identity</b>. Identity kaydı akışın çapasıdır —
/// o anonimleştirildikten sonra bir modül hata verirse geriye kimliği belli olmayan bir kullanıcının
/// yarım verisi kalır ve tekrar denenemez. Bu sırayla, hata hâlinde talep <b>Pending</b> kalır ve
/// sonraki tur baştan dener; tüm anonimleştiriciler idempotent olduğu için tekrar zararsızdır.
/// </para>
/// <para>
/// Modüller Identity'ye proje referansı vermez; buradaki tek bağ <c>Shared.Contracts</c>'teki
/// <see cref="IPersonalDataAnonymizer"/> sözleşmesidir (modül izolasyonu korunur).
/// </para>
/// </summary>
public sealed class AccountDeletionProcessor : IAccountDeletionProcessor
{
    /// <summary>Tek turda uygulanacak talep üst sınırı.</summary>
    private const int BatchSize = 50;

    private readonly IdentityDbContext _dbContext;
    private readonly IReadOnlyCollection<IPersonalDataAnonymizer> _anonymizers;
    private readonly IStudentDirectory _studentDirectory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly ILogger<AccountDeletionProcessor> _logger;

    public AccountDeletionProcessor(
        IdentityDbContext dbContext,
        IEnumerable<IPersonalDataAnonymizer> anonymizers,
        IStudentDirectory studentDirectory,
        IPasswordHasher passwordHasher,
        IIdGenerator idGenerator,
        IClock clock,
        ILogger<AccountDeletionProcessor> logger)
    {
        _dbContext = dbContext;
        _anonymizers = anonymizers.ToArray();
        _studentDirectory = studentDirectory;
        _passwordHasher = passwordHasher;
        _idGenerator = idGenerator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AccountDeletionRunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var due = await _dbContext.AccountDeletionRequests
            .Where(request => request.Status == AccountDeletionRequestStatus.Pending
                && request.ScheduledForUtc <= now)
            .OrderBy(request => request.ScheduledForUtc)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);

        var completed = 0;
        var failed = 0;
        var anonymizedRecords = 0;

        foreach (var request in due)
        {
            try
            {
                anonymizedRecords += await ApplyAsync(request, now, cancellationToken);
                completed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Talep Pending kalır; kaydedilmemiş değişiklikler atılır ki bağlamda yarım durum taşınmasın.
                _dbContext.ChangeTracker.Clear();
                failed++;
                _logger.LogError(
                    exception,
                    "Hesap silme uygulanamadı (talep {RequestId}). Talep bekliyor olarak kaldı, sonraki tur yeniden denenecek.",
                    request.Id);
            }
        }

        if (completed > 0 || failed > 0)
        {
            _logger.LogInformation(
                "Hesap silme turu: {Completed} tamamlandı, {Failed} başarısız, {Records} kayıt anonimleştirildi.",
                completed, failed, anonymizedRecords);
        }

        return new AccountDeletionRunSummary(completed, failed, anonymizedRecords);
    }

    private async Task<int> ApplyAsync(AccountDeletionRequest request, DateTime now, CancellationToken cancellationToken)
    {
        var studentIds = await _studentDirectory.GetStudentIdsForUserAsync(request.UserAccountId, cancellationToken);
        var subject = new PersonalDataSubject(request.UserAccountId, studentIds);

        var affected = 0;
        foreach (var anonymizer in _anonymizers)
        {
            var moduleAffected = await anonymizer.AnonymizeAsync(subject, cancellationToken);
            affected += moduleAffected;
            _logger.LogInformation(
                "Anonimleştirme: modül {Module}, {Records} kayıt (talep {RequestId}).",
                anonymizer.ModuleName, moduleAffected, request.Id);
        }

        // En son Identity: bu kayıt anonimleştikten sonra kullanıcıyı bulmanın yolu kalmaz.
        var user = await _dbContext.UserAccounts
            .Include(account => account.RefreshSessions)
            .Include(account => account.SecurityTokens)
            .FirstOrDefaultAsync(account => account.Id == request.UserAccountId, cancellationToken);

        if (user is not null)
        {
            // Vekil adres ve parola özeti, silinen değerlerden TÜRETİLMEZ (bkz. PersonalDataPlaceholder).
            var anonymized = user.Anonymize(
                PersonalDataPlaceholder.DeletedEmail(_idGenerator.New()),
                _passwordHasher.Hash(_idGenerator.New().ToString()),
                now);

            if (anonymized)
            {
                affected++;
            }
        }

        request.Complete(now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return affected;
    }
}

/// <summary>
/// E-07: Silme turunu düzenli aralıklarla koşturan arka plan servisi.
/// <para>
/// Altı saatte bir koşar: bekleme süresi 30 gün olduğu için gün içi hassasiyet gerekmez, ama
/// süresi dolan bir talebin bir günden fazla beklememesi gerekir.
/// </para>
/// </summary>
internal sealed class AccountDeletionService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(IServiceScopeFactory scopeFactory, ILogger<AccountDeletionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IAccountDeletionProcessor>();
                await processor.RunAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Hesap silme turu başarısız oldu.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
