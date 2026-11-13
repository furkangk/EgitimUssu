namespace EgitimUssu.Shared.Contracts;

// Parents uygular; diğer modüller (Payments) velinin bir öğrencinin ONAYLI velisi olup olmadığını doğrular.
public interface IParentAccessDirectory
{
    Task<bool> IsApprovedParentOfStudentAsync(Guid parentUserId, Guid studentId, CancellationToken cancellationToken);

    /// <summary>Velinin <b>onaylı</b> bağa sahip olduğu öğrenci kimlikleri (M03-3).</summary>
    Task<IReadOnlyCollection<Guid>> ListApprovedStudentIdsAsync(Guid parentUserId, CancellationToken cancellationToken);
}
