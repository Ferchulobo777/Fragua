using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickMetadataService : IMetadataService
{
    public Task<IReadOnlyList<MetadataField>> ReadAsync(ImageAsset input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = new MagickImage(input.SourcePath);
        var exif = image.GetExifProfile();

        var fields = new List<MetadataField>();
        if (exif is not null)
        {
            foreach (var value in exif.Values)
            {
                fields.Add(new MetadataField(value.Tag.ToString(), FormatValue(value)));
            }
        }

        return Task.FromResult<IReadOnlyList<MetadataField>>(fields);
    }

    public Task<ImageAsset> RemoveFieldsAsync(
        ImageAsset input,
        IReadOnlyList<string> tagsToRemove,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(destinationDirectory);

        using var image = new MagickImage(input.SourcePath);
        var exif = image.GetExifProfile();

        if (exif is not null)
        {
            // Juntar los tags a sacar antes de borrar: modificar exif.Values
            // mientras se lo recorre tira una excepcion de coleccion
            // modificada.
            var toRemove = exif.Values
                .Select(v => v.Tag)
                .Where(tag => tagsToRemove.Contains(tag.ToString()))
                .ToList();

            foreach (var tag in toRemove)
            {
                exif.RemoveValue(tag);
            }

            image.SetProfile(exif);
        }

        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(input.SourcePath));
        image.Write(destinationPath);

        var sizeBytes = new FileInfo(destinationPath).Length;

        return Task.FromResult(input with
        {
            SourcePath = destinationPath,
            SizeBytes = sizeBytes,
        });
    }

    private static string FormatValue(IExifValue value)
    {
        var raw = value.GetValue();
        return raw switch
        {
            null => "",
            Array array => string.Join(", ", array.Cast<object>().Select(o => o.ToString())),
            _ => raw.ToString() ?? "",
        };
    }
}
