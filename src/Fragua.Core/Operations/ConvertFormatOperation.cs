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

    public ConvertFormatOperation(IImageAssetWriter writer, string destinationDirectory, ImageFormat format)
    {
        _writer = writer;
        _destinationDirectory = destinationDirectory;
        _format = format;
    }

    public string DisplayName => "Convertir formato";

    public Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken) =>
        _writer.WriteAsync(input, _destinationDirectory, _format, cancellationToken);
}
