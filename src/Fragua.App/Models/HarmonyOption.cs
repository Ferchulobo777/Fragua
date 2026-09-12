using CommunityToolkit.Mvvm.ComponentModel;
using Fragua.Core;

namespace Fragua.App.Models;

/// <summary>Una tarjeta del selector de tipo de armonia en el generador de paletas.</summary>
public sealed partial class HarmonyOption(ColorHarmony value, string badge, string name, string hint) : ObservableObject
{
    public ColorHarmony Value { get; } = value;
    public string Badge { get; } = badge;
    public string Name { get; } = name;
    public string Hint { get; } = hint;

    [ObservableProperty]
    private bool _isSelected;
}
