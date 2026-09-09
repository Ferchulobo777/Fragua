using ImageMagick;

namespace Fragua.Tests.Fixtures;

/// <summary>
/// Genera imagenes de prueba en runtime en vez de commitear binarios al
/// repositorio. Cada test que necesita un archivo real llama a esto y lo
/// limpia en su Dispose.
/// </summary>
internal static class TestImages
{
    public static string CreateSolidPng(int width, int height, string? directory = null)
    {
        var dir = directory ?? Path.Combine(Path.GetTempPath(), "fragua-tests");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"fixture_{Guid.NewGuid():N}.png");

        using var image = new MagickImage(MagickColors.CornflowerBlue, (uint)width, (uint)height);
        image.Write(path, MagickFormat.Png);

        return path;
    }

    /// <summary>
    /// Imagen con ruido de color real (no un color plano), sin comprimir.
    /// Un PNG solido ya sale hipercomprimido por su propio codificador y no
    /// sirve para comparar contra WebP; esto simula lo que de verdad entra
    /// al pipeline: una foto.
    /// </summary>
    public static string CreateNoisyBmp(int width, int height, string? directory = null)
    {
        var dir = directory ?? Path.Combine(Path.GetTempPath(), "fragua-tests");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"fixture_{Guid.NewGuid():N}.bmp");

        var random = new Random(12345);
        var pixels = new ushort[width * height * 3];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (ushort)random.Next(ushort.MaxValue + 1);
        }

        using var image = new MagickImage(MagickColors.Black, (uint)width, (uint)height);
        using var pixelCollection = image.GetPixels();
        pixelCollection.SetArea(0, 0, (uint)width, (uint)height, pixels);
        image.Write(path, MagickFormat.Bmp);

        return path;
    }
}
