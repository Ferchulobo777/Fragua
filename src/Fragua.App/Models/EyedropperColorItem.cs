using Avalonia.Media;

namespace Fragua.App.Models;

/// <summary>El color exacto que saco el cuentagotas de un punto de la imagen.</summary>
public sealed class EyedropperColorItem
{
    public string Hex { get; }
    public string RgbText { get; }
    public ISolidColorBrush Brush { get; }

    public EyedropperColorItem(string hex, byte r, byte g, byte b)
    {
        Hex = hex;
        RgbText = $"rgb({r}, {g}, {b})";
        Brush = new SolidColorBrush(Color.Parse(hex));
    }
}
