using Avalonia.Media;

namespace Fragua.App.Models;

/// <summary>
/// Un tamano de salida conocido para una red social ("Instagram: post",
/// "YouTube: miniatura"). Aplicarlo llena Ancho/Alto y pasa el modo de
/// redimension a Inteligente, para que la imagen quede exacta a esas
/// dimensiones sin distorsionarse. El icono es el logo real de la marca
/// (simple-icons), no un glifo inventado.
/// </summary>
public sealed class SocialPresetOption
{
    private readonly string _iconData;
    private Geometry? _icon;

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public string Hint => $"{Width} × {Height}";

    // Perezoso a proposito: Geometry.Parse necesita el backend de
    // renderizado de Avalonia inicializado (IPlatformRenderInterface). Los
    // tests de xunit construyen ConvertViewModel sin una Application de
    // Avalonia corriendo, y esta coleccion se crea en el constructor del
    // ViewModel; si Icon se resolviera ahi mismo, cualquier test que arme
    // el ViewModel reventaria aunque nunca toque la UI.
    public Geometry Icon => _icon ??= Geometry.Parse(_iconData);

    public IBrush IconBrush { get; }

    public SocialPresetOption(string name, int width, int height, string iconData, string iconHex)
    {
        Name = name;
        Width = width;
        Height = height;
        _iconData = iconData;
        IconBrush = new SolidColorBrush(Color.Parse(iconHex));
    }
}
