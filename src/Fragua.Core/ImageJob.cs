namespace Fragua.Core;

/// <summary>
/// Un trabajo: un archivo origen, una secuencia ordenada de operaciones y un
/// destino. Es la unidad que procesa el pipeline, tanto en conversion
/// individual como en cada fila de un lote.
/// </summary>
public sealed record ImageJob(
    string SourcePath,
    string DestinationDirectory,
    IReadOnlyList<IImageOperation> Operations
);
