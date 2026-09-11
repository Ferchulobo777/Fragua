using Avalonia.Media;

namespace Fragua.App.Models;

/// <summary>Un color de la paleta, ya listo para mostrar (brush) y copiar (hex).</summary>
public sealed class PaletteColorItem
{
    public string Hex { get; }
    public double Percentage { get; }
    public string PercentageText { get; }
    public ISolidColorBrush Brush { get; }

    public PaletteColorItem(string hex, double percentage)
    {
        Hex = hex;
        Percentage = percentage;
        PercentageText = $"{percentage:0.#}%";
        Brush = new SolidColorBrush(Color.Parse(hex));
    }
}
