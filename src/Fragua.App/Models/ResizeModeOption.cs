using CommunityToolkit.Mvvm.ComponentModel;
using Fragua.Core.Operations;

namespace Fragua.App.Models;

/// <summary>Una tarjeta del selector de "Modo de redimension".</summary>
public sealed partial class ResizeModeOption(ResizeMode value, string badge, string name, string hint) : ObservableObject
{
    public ResizeMode Value { get; } = value;
    public string Badge { get; } = badge;
    public string Name { get; } = name;
    public string Hint { get; } = hint;

    [ObservableProperty]
    private bool _isSelected;
}
