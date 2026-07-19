namespace EgitimUssu.Shared.Contracts;

public sealed record ParentPaymentLine(
    Guid Id,
    string Description,
    string Currency,
    decimal ExpectedAmount,
    decimal CollectedAmount,
    DateTime DueDateUtc,
    string Status);

// Payments uygular; veli paneli öğrencinin ödeme kalemlerini (satır düzeyi) canlı okur (Veli V-F).
public interface IStudentPaymentDigestDirectory
{
    Task<IReadOnlyCollection<ParentPaymentLine>> GetLinesAsync(Guid studentId, int take, CancellationToken cancellationToken);
}
