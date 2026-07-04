using System.Text.Json;
using EgitimUssu.Modules.Students.Application;
using EgitimUssu.Shared.Contracts;
using EgitimUssu.Shared.Infrastructure.Messaging;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Modules.Students.Infrastructure;

/// <summary>
/// M09 Parents modülünde bir veli–çocuk bağı onaylandığında (ve birincil veli ise), öğrenci profilinin
/// <c>ParentUserId</c> alanını günceller. Böylece mevcut yetkilendirme (StudentProfilePolicies) veliye
/// öğrenci verisine erişim tanır. Doğrudan cross-module DB erişimi yok — yalnızca integration event.
/// </summary>
internal sealed class ParentChildLinkApprovedIntegrationEventHandler : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IStudentProfileRepository _repository;
    private readonly IClock _clock;

    public ParentChildLinkApprovedIntegrationEventHandler(IStudentProfileRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public bool CanHandle(IIntegrationEvent integrationEvent)
        => integrationEvent.SourceModule == "Parents"
            && integrationEvent.Name == "ParentChildLinkApprovedDomainEvent";

    public async Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (integrationEvent is not IntegrationEvent envelope)
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<ParentChildLinkApprovedPayload>(envelope.Payload, JsonOptions);
        if (payload is null || !payload.IsPrimaryContact)
        {
            return;
        }

        var student = await _repository.GetByIdAsync(payload.StudentId, cancellationToken);
        if (student is null || student.ParentUserId == payload.ParentUserId)
        {
            return;
        }

        student.LinkParent(payload.ParentUserId, _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private sealed record ParentChildLinkApprovedPayload(
        Guid LinkId,
        Guid ParentUserId,
        Guid StudentId,
        bool IsPrimaryContact,
        DateTime ApprovedOnUtc);
}
