using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickColorPaletteExtractor : IColorPaletteExtractor
{
    /// <summary>
    /// Menos de esto de opacidad no es contenido real: es fondo transparente
    /// (el caso mas comun en logos e iconos) y no deberia contar como color
    /// dominante. Sin este filtro, un logo con fondo transparente devuelve
    /// "negro" como color principal, que es simplemente falso.
    /// </summary>
    private const double AlphaThreshold = 0.10;

    public Task<IReadOnlyList<PaletteColor>> ExtractAsync(
        ImageAsset input, int colorCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = new MagickImage(input.SourcePath);
        var hasAlpha = image.HasAlpha;

        // Cuantizar primero: contar sobre millones de colores unicos (uno
        // por pixel con antialiasing) daria una "paleta" de un solo color
        // por franja de pixel, no los tonos dominantes reales.
        image.Quantize(new QuantizeSettings { Colors = (uint)colorCount, ColorSpace = ColorSpace.sRGB });

        var quantumMax = (double)Quantum.Max;
        var pixels = image.GetPixels();
        var counts = new Dictionary<(ushort R, ushort G, ushort B), long>();
        long countedPixels = 0;

        for (var y = 0; y < (int)image.Height; y++)
        {
            for (var x = 0; x < (int)image.Width; x++)
            {
                var p = pixels.GetPixel(x, y).ToArray();
                if (hasAlpha && p[3] / quantumMax < AlphaThreshold)
                {
                    continue;
                }

                var key = (p[0], p[1], p[2]);
                counts[key] = counts.GetValueOrDefault(key) + 1;
                countedPixels++;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (countedPixels == 0)
        {
            // Imagen enteramente transparente: no hay ningun color que reportar.
            return Task.FromResult<IReadOnlyList<PaletteColor>>([]);
        }

        var result = counts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new PaletteColor(
                ToHex(kv.Key.R, kv.Key.G, kv.Key.B, quantumMax),
                Math.Round(kv.Value / (double)countedPixels * 100, 1)))
            .ToList();

        return Task.FromResult<IReadOnlyList<PaletteColor>>(result);
    }

    private static string ToHex(ushort r, ushort g, ushort b, double quantumMax)
    {
        byte To8Bit(ushort v) => (byte)Math.Round(v / quantumMax * 255);
        return $"#{To8Bit(r):X2}{To8Bit(g):X2}{To8Bit(b):X2}";
    }
}
