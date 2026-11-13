namespace EgitimUssu.Shared.Infrastructure.Projections;

/// <summary>
/// Bir projeksiyonu kaynak veriden/outbox geçmişinden yeniden inşa eden bileşen.
/// Her projeksiyon yeniden kurulabilir olmalıdır (K-04); yönetim ucu P12'de eklenecek.
/// </summary>
public interface IProjectionRebuilder
{
    /// <summary>Projeksiyonun kanonik adı (yönetim ucunda kullanılan anahtar).</summary>
    string Name { get; }

    /// <summary>Projeksiyonu sıfırdan kurar ve yazılan satır sayısını döner.</summary>
    Task<int> RebuildAsync(CancellationToken cancellationToken);
}
