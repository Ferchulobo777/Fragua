namespace Fragua.Core;

/// <summary>
/// Lee el <see cref="ImageAsset"/> inicial de un trabajo a partir de la ruta
/// de origen. Es la unica puerta de entrada de archivos reales que el
/// pipeline necesita: el resto de los pasos reciben y devuelven
/// <see cref="ImageAsset"/> en memoria. La implementacion real (Magick.NET)
/// vive en Fragua.Imaging.
/// </summary>
public interface IImageAssetLoader
{
    Task<ImageAsset> LoadAsync(string path, CancellationToken cancellationToken);
}
