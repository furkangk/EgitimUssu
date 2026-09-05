using EgitimUssu.Shared.Infrastructure.Configuration;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>
/// A-04: Kuyruğa alınmış e-postaları arka planda gönderir — outbox dispatcher'ının e-posta karşılığı.
/// <para>
/// Modül başına ayrı kuyruk olduğu için tüm <see cref="ModuleDbContextDescriptor"/>'lar dolaşılır.
/// Her modülün kuyruğu için ayrı kira alınır (A-07): ikinci bir uygulama örneği aynı e-postayı
/// tekrar göndermez.
/// </para>
/// <para>
/// Başarısızlık outbox ile aynı şekilde ele alınır: <c>RetryCount</c> + üstel backoff, eşik
/// aşılınca dead-letter. Dead-letter'a düşmüş satır silinmez — "e-posta gitmedi" bilgisi
/// kaybolmamalıdır.
/// </para>
/// </summary>
public sealed class OutgoingEmailDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    IClock clock,
    ILogger<OutgoingEmailDispatcher> logger) : LeasedBackgroundService(scopeFactory, logger)
{
    private const int ErrorMaxLength = 4000;

    /// <inheritdoc />
    protected override string JobName => "outgoing-email-dispatch";

    /// <inheritdoc />
    protected override TimeSpan PollInterval => TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

    /// <inheritdoc />
    /// <remarks>
    /// Bu iş <b>tüm</b> modüllerin kuyruğunu tek turda boşaltır, dolayısıyla tek bir küresel kira
    /// yeter. Kira, adı alfabetik olarak ilk gelen modülün şemasında tutulur — hangi şema olduğu
    /// önemli değildir, <b>her turda aynı</b> olması önemlidir.
    /// </remarks>
    protected override ModuleDbContext ResolveDbContext(IServiceProvider services)
    {
        var descriptor = services.GetServices<ModuleDbContextDescriptor>()
            .OrderBy(item => item.ModuleName, StringComparer.Ordinal)
            .First();

        return (ModuleDbContext)services.GetRequiredService(descriptor.DbContextType);
    }

    /// <inheritdoc />
    protected override async Task ExecuteCycleAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var sender = services.GetRequiredService<IEmailSender>();

        foreach (var descriptor in services.GetServices<ModuleDbContextDescriptor>())
        {
            try
            {
                var dbContext = (ModuleDbContext)services.GetRequiredService(descriptor.DbContextType);
                await SendPendingAsync(dbContext, sender, settings, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Outbox ile aynı izolasyon: bir modülün tablosu erişilemezse diğerleri durmaz.
                logger.LogError(
                    exception,
                    "E-posta kuyruğu {Module} modülü için işlenemedi; diğer modüllerle devam ediliyor.",
                    descriptor.ModuleName);
            }
        }
    }

    private async Task SendPendingAsync(
        ModuleDbContext dbContext,
        IEmailSender sender,
        OutboxOptions settings,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var pending = await dbContext.OutgoingEmails
            .Where(email => email.SentOnUtc == null
                && email.DeadLetteredOnUtc == null
                && (email.NextAttemptUtc == null || email.NextAttemptUtc <= now))
            .OrderBy(email => email.CreatedOnUtc)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var email in pending)
        {
            try
            {
                await sender.SendAsync(
                    new EmailMessage(email.ToEmail, email.ToEmail, email.Subject, email.HtmlBody, email.TextBody),
                    cancellationToken);

                email.SentOnUtc = clock.UtcNow;
                email.Error = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                RecordFailure(email, exception, settings);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void RecordFailure(OutgoingEmail email, Exception exception, OutboxOptions settings)
    {
        email.RetryCount++;
        email.Error = exception.Message.Length <= ErrorMaxLength
            ? exception.Message
            : exception.Message[..ErrorMaxLength];

        if (email.RetryCount >= settings.MaxRetryCount)
        {
            email.DeadLetteredOnUtc = clock.UtcNow;
            logger.LogError(
                exception,
                "E-posta {EmailId} ({Subject}) {RetryCount} denemeden sonra dead-letter'a taşındı.",
                email.Id,
                email.Subject,
                email.RetryCount);
            return;
        }

        var exponent = Math.Min(email.RetryCount - 1, 30);
        var delay = (long)settings.RetryBackoffBaseSeconds * (1L << exponent);
        email.NextAttemptUtc = clock.UtcNow.AddSeconds(Math.Min(delay, settings.MaxBackoffSeconds));

        logger.LogWarning(
            exception,
            "E-posta {EmailId} gönderilemedi (deneme {RetryCount}); yeniden denenecek.",
            email.Id,
            email.RetryCount);
    }
}
