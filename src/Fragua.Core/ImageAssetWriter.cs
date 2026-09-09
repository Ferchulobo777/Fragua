namespace Fragua.Core;

/// <summary>
/// Escribe un <see cref="ImageAsset"/> a disco en el formato pedido. Lo usa
/// <c>ConvertFormatOperation</c> como ultimo paso real de escritura; el resto
/// del pipeline trabaja en memoria. La implementacion real vive en
/// Fragua.Imaging.
/// </summary>
public interface IImageAssetWriter
{
    Task<ImageAsset> WriteAsync(
        ImageAsset input,
        string destinationDirectory,
        ImageFormat format,
        CancellationToken cancellationToken);
}
