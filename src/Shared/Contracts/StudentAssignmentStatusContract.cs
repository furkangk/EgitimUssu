namespace EgitimUssu.Shared.Contracts;

/// <summary>İptal edilmemiş ödevlerin güncel durum sayıları; teslim edilmiş olanlar tamamlanmış sayılır.</summary>
public sealed record StudentAssignmentStatus(int Total, int Open, int Completed);

/// <summary>M06 kaynak durumu, öğrenci profil kimliğiyle. Parents hiçbir yabancı DbContext okumaz.</summary>
public interface IStudentAssignmentStatusReader
{
    Task<StudentAssignmentStatus> ReadAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken);
}
