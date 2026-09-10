namespace Fragua.Core.Operations;

/// <summary>
/// Cuantos colores distintos traza el vectorizado. Menos colores da un SVG
/// mas limpio y chico: es lo que tiene sentido para logos y arte plano, que
/// es para lo que sirve esto (no para fotografias, ver anti-alcance del
/// plan).
/// </summary>
public sealed record VectorizeSpec(int NumberOfColors = 16);

/// <summary>
/// Vectorizar es terminal, como ConvertFormatOperation: la salida es un
/// archivo de texto (SVG), no una transformacion en memoria que otro paso
/// pueda seguir procesando como raster.
/// </summary>
public interface IImageVectorizer
{
    Task<ImageAsset> VectorizeAsync(
        ImageAsset input,
        VectorizeSpec spec,
        string destinationDirectory,
        CancellationToken cancellationToken);
}

public sealed class VectorizeOperation : IImageOperation
{
    private readonly IImageVectorizer _vectorizer;
    private readonly VectorizeSpec _spec;
    private readonly string _destinationDirectory;

    public VectorizeOperation(IImageVectorizer vectorizer, VectorizeSpec spec, string destinationDirectory)
    {
        _vectorizer = vectorizer;
        _spec = spec;
        _destinationDirectory = destinationDirectory;
    }

    public string DisplayName => "Vectorizar";

    public Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken) =>
        _vectorizer.VectorizeAsync(input, _spec, _destinationDirectory, cancellationToken);
}
