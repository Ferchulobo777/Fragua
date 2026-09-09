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
}
