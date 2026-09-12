using Avalonia.Media;

namespace Fragua.App.Models;

/// <summary>
/// Un color mostrado como muestra: el pixel exacto que saco el cuentagotas,
/// o uno de los colores que arma el generador de paletas por armonia.
/// </summary>
public sealed class ColorSwatchItem
{
    public string Hex { get; }
    public string RgbText { get; }
    public ISolidColorBrush Brush { get; }

    public ColorSwatchItem(string hex)
    {
        Hex = hex;
        var color = Color.Parse(hex);
        RgbText = $"rgb({color.R}, {color.G}, {color.B})";
        Brush = new SolidColorBrush(color);
    }
}
