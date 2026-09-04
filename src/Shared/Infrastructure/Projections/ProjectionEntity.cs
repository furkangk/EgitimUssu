namespace EgitimUssu.Shared.Infrastructure.Projections;

/// <summary>
/// Event-beslemeli read-model (projeksiyon) satırlarının ortak tabanı (K-04).
/// Her projeksiyon, kendisini en son güncelleyen event'i taşır; böylece bir satırın
/// hangi noktaya kadar beslendiği (ve yeniden inşa sırasında nereden devam edileceği) görülebilir.
/// </summary>
public abstract class ProjectionEntity
{
    /// <summary>Bu satırı en son güncelleyen integration event'in kimliği.</summary>
    public Guid LastAppliedEventId { get; protected set; }

    /// <summary>Bu satırın en son güncellendiği an (UTC).</summary>
    public DateTime UpdatedOnUtc { get; protected set; }

    /// <summary>Projeksiyonu besleyen event uygulandıktan sonra çağrılır.</summary>
    public void MarkApplied(Guid eventId, DateTime nowUtc)
    {
        LastAppliedEventId = eventId;
        UpdatedOnUtc = nowUtc;
    }
}
