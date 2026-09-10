namespace Fragua.Core;

/// <summary>
/// Formatos de salida que el pipeline sabe producir. No es la lista completa
/// de formatos que Magick.NET puede leer (esos entran como origen sin
/// restriccion): es la lista de a que se puede convertir.
/// </summary>
public enum ImageFormat
{
    Png,
    Jpeg,
    WebP,
    Avif,
    Tiff,
    Bmp,

    /// <summary>
    /// Salida de Vectorizar, no de Convertir formato: no aparece en el
    /// selector de "Formato de salida" (eso sigue siendo solo raster).
    /// Existe aca para que ImageAsset tenga un solo tipo de resultado en
    /// todo el pipeline, en vez de un tipo paralelo solo para vectores.
    /// </summary>
    Svg,
}
