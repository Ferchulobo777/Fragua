namespace Fragua.Core.Operations;

/// <summary>Un campo de metadatos EXIF tal como esta en el archivo: nombre y valor legible.</summary>
public sealed record MetadataField(string Tag, string Value);

/// <summary>
/// Lee y quita metadatos EXIF de forma selectiva: a diferencia de Optimizar
/// (que los saca todos con Strip), esto deja elegir campo por campo. La
/// implementacion real (Magick.NET) vive en Fragua.Imaging.
/// </summary>
public interface IMetadataService
{
    Task<IReadOnlyList<MetadataField>> ReadAsync(ImageAsset input, CancellationToken cancellationToken);

    Task<ImageAsset> RemoveFieldsAsync(
        ImageAsset input,
        IReadOnlyList<string> tagsToRemove,
        string destinationDirectory,
        CancellationToken cancellationToken);
}
