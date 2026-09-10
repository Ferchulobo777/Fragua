using System.Collections.Generic;
using System.Linq;

namespace Fragua.Imaging.ImageTrace;

// Port de ColorReference.cs de MiYanni/ImageTracer.NET (The Unlicense), sin
// la dependencia de System.Drawing.Color: los cuatro bytes se guardan
// directos. Deliberadamente una clase, no un record ni un struct, y sin
// Equals/GetHashCode: el algoritmo de vectorizado compara colores por
// identidad de referencia (dos instancias con el mismo RGBA pero distintas
// no son "el mismo color" para el, y eso es correcto porque todo el
// pipeline solo compara instancias que vienen de la misma paleta finita).
internal sealed class ColorReference
{
    public byte A { get; }
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    public ColorReference(byte alpha, byte red, byte green, byte blue)
    {
        A = alpha;
        R = red;
        G = green;
        B = blue;
    }

    private ColorReference()
    {
    }

    public static ColorReference Empty { get; } = new();

    // https://en.wikipedia.org/wiki/Rectilinear_distance
    // El peso x4 en el canal alfa ayuda con imagenes que tienen transparencia.
    public int CalculateRectilinearDistance(ColorReference other)
    {
        return System.Math.Abs(A - other.A) * 4
            + System.Math.Abs(R - other.R)
            + System.Math.Abs(G - other.G)
            + System.Math.Abs(B - other.B);
    }

    // Busca el color mas cercano de la paleta por distancia rectilinea.
    public ColorReference FindClosest(IReadOnlyList<ColorReference> palette)
    {
        var distance = 256 * 4;
        var paletteColor = palette.First();
        foreach (var color in palette)
        {
            var newDistance = color.CalculateRectilinearDistance(this);
            if (newDistance >= distance) continue;

            distance = newDistance;
            paletteColor = color;
        }
        return paletteColor;
    }

    public string ToSvgString()
    {
        return $"fill=\"rgb({R},{G},{B})\" stroke=\"rgb({R},{G},{B})\" stroke-width=\"1\" opacity=\"{A / 255.0}\" ";
    }
}
