using CommunityToolkit.Mvvm.ComponentModel;
using Fragua.Core.Operations;

namespace Fragua.App.Models;

/// <summary>Una tarjeta del selector de posicion de la marca de agua.</summary>
public sealed partial class WatermarkPositionOption(WatermarkPosition value, string badge, string name) : ObservableObject
{
    public WatermarkPosition Value { get; } = value;
    public string Badge { get; } = badge;
    public string Name { get; } = name;

    [ObservableProperty]
    private bool _isSelected;
}
