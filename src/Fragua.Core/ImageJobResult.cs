namespace Fragua.Core;

/// <summary>
/// Resultado de procesar un <see cref="ImageJob"/> completo. En un lote, un
/// error en un archivo no corta a los demas: cada trabajo produce el suyo
/// y el lote sigue.
/// </summary>
public sealed record ImageJobResult
{
    public required ImageJob Job { get; init; }
    public ImageAsset? Output { get; init; }
    public string? ErrorMessage { get; init; }
    public bool WasCancelled { get; init; }

    public bool Succeeded => Output is not null && ErrorMessage is null && !WasCancelled;

    public static ImageJobResult Success(ImageJob job, ImageAsset output) =>
        new() { Job = job, Output = output };

    public static ImageJobResult Failure(ImageJob job, string errorMessage) =>
        new() { Job = job, ErrorMessage = errorMessage };

    public static ImageJobResult Cancelled(ImageJob job) =>
        new() { Job = job, WasCancelled = true };
}
