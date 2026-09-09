using Fragua.Core;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickImageAssetWriter : IImageAssetWriter
{
    public Task<ImageAsset> WriteAsync(
        ImageAsset input,
        string destinationDirectory,
        ImageFormat format,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(destinationDirectory);

        using var image = new MagickImage(input.SourcePath);
        image.Format = FormatMapper.ToMagick(format);

        var fileName = Path.GetFileNameWithoutExtension(input.SourcePath) + FormatMapper.Extension(format);
        var destinationPath = Path.Combine(destinationDirectory, fileName);
        image.Write(destinationPath);

        // Un paso intermedio (por ejemplo Redimensionar) deja su resultado en
        // un archivo temporal. Una vez escrito el destino final, ese temporal
        // no sirve para nada mas: se limpia para no ensuciar %TEMP%.
        if (IsTempFile(input.SourcePath))
        {
            TryDelete(input.SourcePath);
        }

        var sizeBytes = new FileInfo(destinationPath).Length;

        return Task.FromResult(input with
        {
            SourcePath = destinationPath,
            WidthPixels = (int)image.Width,
            HeightPixels = (int)image.Height,
            SizeBytes = sizeBytes,
            Format = format,
        });
    }

    private static bool IsTempFile(string path) =>
        Path.GetFileName(path).StartsWith("fragua_", StringComparison.Ordinal)
        && string.Equals(Path.GetDirectoryName(path), Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // El archivo temporal se limpia en el proximo arranque si esta
            // en uso ahora; no vale la pena fallar la conversion por esto.
        }
    }
}
