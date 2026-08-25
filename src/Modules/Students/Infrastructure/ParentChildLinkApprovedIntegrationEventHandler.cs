using System.Text.Json;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace EgitimUssu.Modules.Students.Infrastructure;

/// <summary>
/// M09 Parents modülünde bir veli–çocuk bağı onaylandığında (ve birincil veli ise), öğrenci profilinin
/// <c>ParentUserId</c> alanını günceller. Böylece mevcut yetkilendirme (StudentProfilePolicies) veliye
/// öğrenci verisine erişim tanır. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// Replay koruması artık ortak inbox üzerinden (<see cref="IdempotentIntegrationEventHandler"/>,
/// EventId+Handler); "zaten aynı veli bağlı" iş guard'ı burada korunur — <c>LinkParent</c> domain
/// çağrısını (ve gereksiz UpdatedOnUtc dokunuşunu) tekrar tetiklememek için gerçek bir iş kuralıdır.
/// </summary>
internal sealed class ParentChildLinkApprovedIntegrationEventHandler : IdempotentIntegrationEventHandler
{
    public ParentChildLinkApprovedIntegrationEventHandler(StudentsDbContext dbContext, IClock clock)
        : base(dbContext, clock)
    {
    }

    private StudentsDbContext StudentsDb => (StudentsDbContext)DbContext;

    public override bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Parents"
            && integrationEvent.Name == "ParentChildLinkApprovedDomainEvent";

    protected override async Task<bool> ApplyAsync(IntegrationEvent envelope, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ParentChildLinkApprovedPayload>(envelope.Payload, IntegrationEventSerialization.Options);
        if (payload is null || !payload.IsPrimaryContact)
        {
            return false;
        }

        var student = await StudentsDb.StudentProfiles
            .Include(profile => profile.Subjects)
            .FirstOrDefaultAsync(profile => profile.Id == payload.StudentId, cancellationToken);
        if (student is null || student.ParentUserId == payload.ParentUserId)
        {
            return false;
        }

        student.LinkParent(payload.ParentUserId, Clock.UtcNow);
        return true;
    }

    private sealed record ParentChildLinkApprovedPayload(
        Guid LinkId,
        Guid ParentUserId,
        Guid StudentId,
        bool IsPrimaryContact,
        DateTime ApprovedOnUtc);
}
