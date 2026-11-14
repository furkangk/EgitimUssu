using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Tests.Unit.TestDoubles;

public sealed class FakeStudentDirectory(params Guid[] studentIds) : IStudentDirectory
{
    public Task<IReadOnlyCollection<Guid>> GetStudentIdsForUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>(studentIds);
    public Task<Guid?> GetOwnerUserIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Guid?>(null);
    public Task<AgreedLessonRate?> GetAgreedLessonRateAsync(Guid teacher, Guid id, CancellationToken ct) => Task.FromResult<AgreedLessonRate?>(null);
    public Task<bool> IsLinkedTeacherOfStudentAsync(Guid teacher, Guid id, CancellationToken ct) => Task.FromResult(false);
    public Task<string?> GetDisplayNameAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>(null);
}
