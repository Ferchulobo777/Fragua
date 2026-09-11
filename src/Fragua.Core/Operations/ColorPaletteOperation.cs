namespace Fragua.Core.Operations;

/// <summary>Un color de la paleta y que porcentaje del contenido visible ocupa.</summary>
public sealed record PaletteColor(string Hex, double Percentage);

/// <summary>
/// Extrae los colores dominantes de una imagen. Los pixeles transparentes
/// no cuentan como color: son ausencia de contenido, no un color de fondo.
/// La implementacion real (Magick.NET) vive en Fragua.Imaging.
/// </summary>
public interface IColorPaletteExtractor
{
    Task<IReadOnlyList<PaletteColor>> ExtractAsync(ImageAsset input, int colorCount, CancellationToken cancellationToken);
}
