namespace Fragua.Core.Operations;

public enum ResizeMode
{
    /// <summary>Ancho y alto exactos, puede distorsionar la proporcion.</summary>
    Exact,
    /// <summary>Ajusta manteniendo proporcion, el resultado entra dentro del ancho y alto dados.</summary>
    Fit,
    /// <summary>Porcentaje del tamano original.</summary>
    Percentage,
}

/// <summary>
/// Datos de una redimension, separados de quien la ejecuta. Un test unitario
/// del pipeline arma uno de estos sin tocar ninguna libreria de imagenes.
/// </summary>
public sealed record ResizeSpec(
    ResizeMode Mode,
    int? Width = null,
    int? Height = null,
    double? Percentage = null
);

/// <summary>
/// Redimensiona en memoria. La implementacion real (Magick.NET) vive en
/// Fragua.Imaging.
/// </summary>
public interface IImageResizer
{
    Task<ImageAsset> ResizeAsync(ImageAsset input, ResizeSpec spec, CancellationToken cancellationToken);
}

public sealed class ResizeOperation : IImageOperation
{
    private readonly IImageResizer _resizer;
    private readonly ResizeSpec _spec;

    public ResizeOperation(IImageResizer resizer, ResizeSpec spec)
    {
        _resizer = resizer;
        _spec = spec;
    }

    public string DisplayName => "Redimensionar";

    public Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken) =>
        _resizer.ResizeAsync(input, _spec, cancellationToken);
}
