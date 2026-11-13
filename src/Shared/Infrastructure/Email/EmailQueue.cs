using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure.Email;

/// <summary>A-04: <see cref="IEmailQueue"/>'nun <c>outgoing_emails</c> tablosuna yazan uygulaması.</summary>
public sealed class EmailQueue(IIdGenerator idGenerator, IClock clock) : IEmailQueue
{
    /// <inheritdoc />
    public void Enqueue(ModuleDbContext dbContext, EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(message);

        dbContext.OutgoingEmails.Add(new OutgoingEmail
        {
            Id = idGenerator.New(),
            ToEmail = message.ToEmail,
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
            CreatedOnUtc = clock.UtcNow,
        });
    }
}
