namespace EgitimUssu.Shared.Kernel;

/// <summary>Veritabanının ürettiği iyimser eşzamanlılık sürümünü taşıyan kayıt.</summary>
public interface IVersioned
{
    uint Version { get; }
}
