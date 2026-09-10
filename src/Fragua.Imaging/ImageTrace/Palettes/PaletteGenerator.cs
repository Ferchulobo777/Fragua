using System;
using System.Collections.Generic;
using System.Linq;
using Fragua.Imaging.ImageTrace.Extensions;

namespace Fragua.Imaging.ImageTrace.Palettes;

// Port de Palettes/PaletteGenerator.cs de MiYanni/ImageTracer.NET (The
// Unlicense). Genera una paleta sintetica (cubo RGB + relleno aleatorio):
// no depende de la imagen de entrada, asi que no hace falta portar
// SmartPalette ni GaussianBlur (que si dependian de System.Drawing.Bitmap).
internal static class PaletteGenerator
{
    private static readonly Random Rng = new();

    private static ColorReference RandomColor()
    {
        return new ColorReference(
            (byte)Rng.Next(256),
            (byte)Rng.Next(256),
            (byte)Rng.Next(256),
            (byte)Rng.Next(256));
    }

    private static ColorReference[] GenerateGrayscale(int numberOfColors)
    {
        var step = 255 / (numberOfColors - 1);
        return new ColorReference[numberOfColors].Initialize(i =>
        {
            var component = (byte)(i * step);
            return new ColorReference(255, component, component, component);
        });
    }

    private static IEnumerable<ColorReference> GenerateRgbCube(int numberOfColors)
    {
        var colorQNum = (int)Math.Floor(Math.Pow(numberOfColors, 1.0 / 3.0));
        // Bug real del original: dividia 255 por (numberOfColors-1) en vez
        // de por (colorQNum-1), asi que el "cubo" quedaba comprimido en una
        // esquina oscura del espacio de color (para numberOfColors=8 los
        // unicos valores posibles eran 0 y 36) en vez de cubrir 0..255.
        var step = colorQNum > 1 ? 255 / (colorQNum - 1) : 255;

        for (var redCount = 0; redCount < colorQNum; redCount++)
        {
            for (var greenCount = 0; greenCount < colorQNum; greenCount++)
            {
                for (var blueCount = 0; blueCount < colorQNum; blueCount++)
                {
                    yield return new ColorReference(255, (byte)(redCount * step), (byte)(greenCount * step), (byte)(blueCount * step));
                }
            }
        }
    }

    // array[numberofcolors] de ColorReference: parte cubo RGB, el resto aleatorio.
    public static ColorReference[] GeneratePalette(int numberOfColors)
    {
        if (numberOfColors < 8)
        {
            return GenerateGrayscale(numberOfColors);
        }

        var colorQNumTotal = (int)Math.Floor(Math.Pow(numberOfColors, 1.0 / 3.0)) * 3;
        var rgbCube = GenerateRgbCube(numberOfColors).ToList();

        return new ColorReference[numberOfColors].Initialize(i => i < colorQNumTotal ? rgbCube[i % rgbCube.Count] : RandomColor());
    }
}
