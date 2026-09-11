namespace Fragua.Core.Operations;

/// <summary>
/// Como armar el collage/sprite sheet: cuantas columnas, el tamano de cada
/// celda cuadrada, el espacio entre celdas (0 para un sprite sheet bien
/// pegado), si recortar cada imagen para llenar su celda o mostrarla
/// completa, y si el fondo sobrante queda transparente o blanco.
/// </summary>
public sealed record CollageSpec(
    IReadOnlyList<string> SourcePaths,
    int Columns,
    uint CellSize,
    int SpacingPixels,
    bool CropToFill,
    bool TransparentBackground);

public sealed record CollageResult(string OutputPath, uint Width, uint Height);

public interface ICollageComposer
{
    Task<CollageResult> ComposeAsync(
        CollageSpec spec,
        string destinationDirectory,
        CancellationToken cancellationToken);
}
