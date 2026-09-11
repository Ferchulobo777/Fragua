namespace Fragua.Core.Operations;

/// <summary>
/// Que tamanos generar y si ademas empaquetar un .ico de Windows con todos
/// los tamanos aptos (Windows no acepta 512 dentro de un .ico).
/// </summary>
public sealed record IconSetSpec(IReadOnlyList<int> Sizes, bool IncludeIco);

/// <summary>
/// A diferencia del resto de las operaciones, esta no devuelve un solo
/// ImageAsset: de una imagen salen varios archivos (un PNG por tamano, mas
/// el .ico combinado), asi que no encaja en IImageOperation.
/// </summary>
public sealed record IconSetResult(IReadOnlyList<string> GeneratedFiles);

public interface IIconSetGenerator
{
    Task<IconSetResult> GenerateAsync(
        ImageAsset input,
        IconSetSpec spec,
        string destinationDirectory,
        CancellationToken cancellationToken);
}
