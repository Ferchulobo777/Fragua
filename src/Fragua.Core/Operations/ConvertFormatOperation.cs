namespace Fragua.Core.Operations;

/// <summary>
/// Ultimo paso tipico del pipeline: escribe el resultado a disco en el
/// formato pedido. Usa <see cref="IImageAssetWriter"/> (Fragua.Imaging hace
/// la escritura real con Magick.NET).
/// </summary>
public sealed class ConvertFormatOperation : IImageOperation
{
    private readonly IImageAssetWriter _writer;
    private readonly string _destinationDirectory;
    private readonly ImageFormat _format;
    private readonly OptimizeSpec? _optimize;

    public ConvertFormatOperation(
        IImageAssetWriter writer,
        string destinationDirectory,
        ImageFormat format,
        OptimizeSpec? optimize = null)
    {
        _writer = writer;
        _destinationDirectory = destinationDirectory;
        _format = format;
        _optimize = optimize;
    }

    public string DisplayName => _optimize is null ? "Convertir formato" : "Convertir y optimizar";

    public Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken) =>
        _writer.WriteAsync(input, _destinationDirectory, _format, _optimize, cancellationToken);
}
