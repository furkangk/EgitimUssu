using Microsoft.AspNetCore.Builder;

namespace EgitimUssu.Shared.Infrastructure.Modules;

/// <summary>
/// C-10: Yayınlanan API sözleşmesinin sürümleri.
/// <para>
/// Sürüm, derleme sürümünden (<c>1.4.2</c> gibi) <b>bağımsızdır</b>: derleme her yayında değişir,
/// sözleşme ise yalnız kırıcı bir değişiklikte. İstemcinin izlemesi gereken sayı budur.
/// </para>
/// <para>Politika: <see href="../../../../doc/architecture/api_versioning.md">api_versioning.md</see>.</para>
/// </summary>
public static class ApiVersions
{
    /// <summary>Bugünkü sözleşme. Mevcut <c>/api/...</c> yolları bu sürümdür (yol eki taşımazlar).</summary>
    public const string V1 = "v1";

    /// <summary>Sürüm eki olmayan uçların bildirdiği sürüm.</summary>
    public const string Current = V1;
}

/// <summary>
/// Bir ucun (ya da uç grubunun) hangi sözleşme sürümüne ait olduğunu söyleyen endpoint metadata'sı.
/// <c>/api/v2/...</c> grubu açıldığında sürüm başlığı bu etiketten okunur; başka hiçbir yeri
/// değiştirmek gerekmez.
/// </summary>
/// <param name="Version">Sözleşme sürümü (<see cref="ApiVersions"/>).</param>
public sealed record ApiVersionMetadata(string Version);

public static class ApiVersionEndpointExtensions
{
    /// <summary>Uç grubunu bir sözleşme sürümüyle etiketler.</summary>
    public static TBuilder WithApiVersion<TBuilder>(this TBuilder builder, string version)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new ApiVersionMetadata(version));
        return builder;
    }
}
