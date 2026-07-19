namespace EgitimUssu.Shared.Contracts;

// Parents uygular; diğer modüller (Payments) velinin bir öğrencinin ONAYLI velisi olup olmadığını doğrular.
public interface IParentAccessDirectory
{
    Task<bool> IsApprovedParentOfStudentAsync(Guid parentUserId, Guid studentId, CancellationToken cancellationToken);
}
