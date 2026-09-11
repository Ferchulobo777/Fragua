using CommunityToolkit.Mvvm.ComponentModel;

namespace Fragua.App.Models;

/// <summary>Una fila de la lista de metadatos: el campo, su valor, y si esta marcado para sacar.</summary>
public sealed partial class MetadataFieldItem(string tag, string value) : ObservableObject
{
    public string Tag { get; } = tag;
    public string Value { get; } = value;

    [ObservableProperty]
    private bool _isSelected;
}
