using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

/// <summary>
/// P15 / N-26: M09 Parents'ta bir veli–çocuk bağı bittiğinde (ret ya da iptal) öğrenci profilinin <c>ParentUserId</c>'sini
/// temizler — yalnız alan o veliyi gösteriyorsa. Eskiden alan yalnız onay olayında yazılıyor, bitiş hiç tüketilmiyordu.
/// <para>
/// Erişim kararı bu alana bağlı değildir (profil okuma onaylı bağı <see cref="IParentAccessDirectory"/>'den okur, iptal
/// anında kesilir); tüketici türetilmiş alanı ve onu gösteren yanıtları tutarlı tutar. Doğrudan cross-module DB erişimi
/// yok — yalnız integration event. Replay koruması ortak inbox üzerinden.
/// </para>
/// </summary>
internal sealed class ParentChildLinkEndedIntegrationEventHandler : IdempotentIntegrationEventHandler
{
    public ParentChildLinkEndedIntegrationEventHandler(StudentsDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private StudentsDbContext StudentsDb => (StudentsDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Parents"
            && integrationEvent.Name == "ParentChildLinkEndedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ParentChildLinkEndedIntegrationEvent>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null)
        {
            throw new PoisonMessageException("Olay yükü null olamaz.");
        }

        var student = await StudentsDb.StudentProfiles
            .Include(profile => profile.Subjects)
            .FirstOrDefaultAsync(profile => profile.Id == payload.StudentId, cancellationToken);
        student?.UnlinkParent(payload.ParentUserId, Clock.UtcNow);
        return true;
    }
}
