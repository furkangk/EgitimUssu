using EgitimUssu.Shared.Contracts;
namespace EgitimUssu.Tests.Unit.TestDoubles;
public sealed class FakeCancellationPolicyDirectory : ICancellationPolicyDirectory
{
    public Task<TeacherCancellationPolicy> GetCancellationPolicyAsync(Guid teacher, CancellationToken ct)
        => Task.FromResult(TeacherCancellationPolicy.Disabled);
}
