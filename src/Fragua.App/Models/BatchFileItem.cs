using CommunityToolkit.Mvvm.ComponentModel;

namespace Fragua.App.Models;

public enum BatchFileStatus
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <summary>
/// Una fila de la tabla de Lotes: un archivo y su estado real, no una barra
/// que avanza sola. El estado cambia a medida que el pipeline lo procesa.
/// </summary>
public sealed partial class BatchFileItem : ObservableObject
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }

    [ObservableProperty]
    private BatchFileStatus _status = BatchFileStatus.Pending;

    [ObservableProperty]
    private string? _detail;
}
