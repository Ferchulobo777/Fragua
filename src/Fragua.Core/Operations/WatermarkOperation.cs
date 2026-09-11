namespace Fragua.Core.Operations;

public enum WatermarkPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center,
}

/// <summary>Opacity va de 0 (invisible) a 1 (solido).</summary>
public sealed record WatermarkSpec(string Text, WatermarkPosition Position, double Opacity);

/// <summary>
/// Marca de agua de texto. La implementacion real (Magick.NET) vive en
/// Fragua.Imaging.
/// </summary>
public interface IWatermarker
{
    Task<ImageAsset> ApplyAsync(ImageAsset input, WatermarkSpec spec, CancellationToken cancellationToken);
}

/// <summary>
/// Va despues de Redimensionar (el texto tiene que escalar con el tamano
/// final, no con el original) y antes de Convertir formato / Vectorizar:
/// no aplica sobre un SVG, es un efecto raster.
/// </summary>
public sealed class WatermarkOperation : IImageOperation
{
    private readonly IWatermarker _watermarker;
    private readonly WatermarkSpec _spec;

    public WatermarkOperation(IWatermarker watermarker, WatermarkSpec spec)
    {
        _watermarker = watermarker;
        _spec = spec;
    }

    public string DisplayName => "Marca de agua";

    public Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken) =>
        _watermarker.ApplyAsync(input, _spec, cancellationToken);
}
