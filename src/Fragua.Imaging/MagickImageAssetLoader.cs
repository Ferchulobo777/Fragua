using Fragua.Core;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickImageAssetLoader : IImageAssetLoader
{
    public Task<ImageAsset> LoadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = new MagickImage(path);
        var sizeBytes = new FileInfo(path).Length;
        var format = FormatMapper.FromMagick(image.Format) ?? FormatMapper.FromExtension(path);

        return Task.FromResult(new ImageAsset(
            SourcePath: path,
            WidthPixels: (int)image.Width,
            HeightPixels: (int)image.Height,
            SizeBytes: sizeBytes,
            Format: format));
    }
}
