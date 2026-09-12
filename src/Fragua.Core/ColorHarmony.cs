namespace Fragua.Core;

public enum ColorHarmony
{
    Complementary,
    Analogous,
    Triadic,
    Monochromatic,
}

/// <summary>
/// Genera paletas a partir de un color base con matematica de color pura
/// (rotacion de matiz en HSL), sin abrir ningun archivo. No necesita
/// Magick.NET: es la unica operacion de la app que no toca disco.
/// </summary>
public static class ColorHarmonyGenerator
{
    private static readonly double[] MonochromaticLightnessSteps = [0.15, 0.32, 0.5, 0.68, 0.85];

    public static IReadOnlyList<string> Generate(string baseHex, ColorHarmony harmony)
    {
        var (h, s, l) = HexToHsl(baseHex);

        return harmony switch
        {
            ColorHarmony.Complementary =>
            [
                HslToHex(h, s, l),
                HslToHex(h + 180, s, l),
            ],
            ColorHarmony.Analogous =>
            [
                HslToHex(h - 30, s, l),
                HslToHex(h, s, l),
                HslToHex(h + 30, s, l),
            ],
            ColorHarmony.Triadic =>
            [
                HslToHex(h, s, l),
                HslToHex(h + 120, s, l),
                HslToHex(h + 240, s, l),
            ],
            ColorHarmony.Monochromatic =>
                MonochromaticLightnessSteps.Select(lightness => HslToHex(h, s, lightness)).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(harmony)),
        };
    }

    private static (double H, double S, double L) HexToHsl(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToInt32(hex[..2], 16) / 255.0;
        var g = Convert.ToInt32(hex[2..4], 16) / 255.0;
        var b = Convert.ToInt32(hex[4..6], 16) / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;

        if (max == min)
        {
            return (0, 0, l);
        }

        var delta = max - min;
        var s = l > 0.5 ? delta / (2 - max - min) : delta / (max + min);

        double h;
        if (max == r)
        {
            h = (g - b) / delta + (g < b ? 6 : 0);
        }
        else if (max == g)
        {
            h = (b - r) / delta + 2;
        }
        else
        {
            h = (r - g) / delta + 4;
        }

        return (h * 60, s, l);
    }

    private static string HslToHex(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;

        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h / 360 + 1.0 / 3);
            g = HueToRgb(p, q, h / 360);
            b = HueToRgb(p, q, h / 360 - 1.0 / 3);
        }

        byte To8Bit(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return $"#{To8Bit(r):X2}{To8Bit(g):X2}{To8Bit(b):X2}";
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
