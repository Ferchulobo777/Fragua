using CommunityToolkit.Mvvm.ComponentModel;
using Fragua.Core;

namespace Fragua.App.Models;

/// <summary>
/// Una tarjeta del selector de "Formato de salida". Badge y Hint son texto
/// fijo pensado para el usuario final, no el nombre tecnico del enum.
/// </summary>
public sealed partial class FormatOption(ImageFormat value, string badge, string name, string hint) : ObservableObject
{
    public ImageFormat Value { get; } = value;
    public string Badge { get; } = badge;
    public string Name { get; } = name;
    public string Hint { get; } = hint;

    [ObservableProperty]
    private bool _isSelected;
}
