using Fragua.Core;
using ImageMagick;

namespace Fragua.Imaging;

/// <summary>
/// Traduce entre el <see cref="ImageFormat"/> de Core (la lista de a que se
/// puede convertir) y el <see cref="MagickFormat"/> de la libreria real.
/// </summary>
internal static class FormatMapper
{
    public static MagickFormat ToMagick(ImageFormat format) => format switch
    {
        ImageFormat.Png => MagickFormat.Png,
        ImageFormat.Jpeg => MagickFormat.Jpg,
        ImageFormat.WebP => MagickFormat.WebP,
        ImageFormat.Avif => MagickFormat.Avif,
        ImageFormat.Tiff => MagickFormat.Tiff,
        ImageFormat.Bmp => MagickFormat.Bmp,
        _ => throw new NotSupportedException($"Formato no soportado: {format}"),
    };

    public static ImageFormat? FromMagick(MagickFormat format) => format switch
    {
        MagickFormat.Png or MagickFormat.Png00 or MagickFormat.Png8 or MagickFormat.Png24 or MagickFormat.Png32 => ImageFormat.Png,
        MagickFormat.Jpg or MagickFormat.Jpeg => ImageFormat.Jpeg,
        MagickFormat.WebP => ImageFormat.WebP,
        MagickFormat.Avif => ImageFormat.Avif,
        MagickFormat.Tiff or MagickFormat.Tif => ImageFormat.Tiff,
        MagickFormat.Bmp or MagickFormat.Bmp2 or MagickFormat.Bmp3 => ImageFormat.Bmp,
        _ => null,
    };

    public static ImageFormat FromExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => ImageFormat.Png,
        ".jpg" or ".jpeg" => ImageFormat.Jpeg,
        ".webp" => ImageFormat.WebP,
        ".avif" => ImageFormat.Avif,
        ".tif" or ".tiff" => ImageFormat.Tiff,
        ".bmp" => ImageFormat.Bmp,
        _ => ImageFormat.Png,
    };

    public static string Extension(ImageFormat format) => format switch
    {
        ImageFormat.Png => ".png",
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.WebP => ".webp",
        ImageFormat.Avif => ".avif",
        ImageFormat.Tiff => ".tiff",
        ImageFormat.Bmp => ".bmp",
        _ => ".png",
    };
}
