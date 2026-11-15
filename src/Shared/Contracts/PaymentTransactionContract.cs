namespace EgitimUssu.Shared.Contracts;

/// <summary>Tahsilat projeksiyonunun yeniden kurulabilen kanonik hareket kaynağı; öğrenci kişisel verisi taşımaz.</summary>
public interface IPaymentTransactionDirectory
{
    Task<IReadOnlyCollection<PaymentTransactionLine>> ListAsync(CancellationToken cancellationToken);
}

public sealed record PaymentTransactionLine(Guid TransactionId, Guid TeacherUserId, bool IsRefund,
    decimal Amount, string Currency, DateTime OccurredOnUtc);
