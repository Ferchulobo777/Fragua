using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickImageResizer : IImageResizer
{
    public Task<ImageAsset> ResizeAsync(ImageAsset input, ResizeSpec spec, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = new MagickImage(input.SourcePath);

        var geometry = spec.Mode switch
        {
            ResizeMode.Exact => new MagickGeometry(
                (uint)RequireDimension(spec.Width, nameof(spec.Width)),
                (uint)RequireDimension(spec.Height, nameof(spec.Height)))
            {
                IgnoreAspectRatio = true,
            },
            ResizeMode.Fit => new MagickGeometry(
                (uint)RequireDimension(spec.Width, nameof(spec.Width)),
                (uint)RequireDimension(spec.Height, nameof(spec.Height))),
            ResizeMode.Percentage => PercentageGeometry(
                spec.Percentage ?? throw new ArgumentException("Falta el porcentaje.", nameof(spec))),
            _ => throw new NotSupportedException($"Modo de redimension no soportado: {spec.Mode}"),
        };

        image.Resize(geometry);

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"fragua_{Guid.NewGuid():N}{Path.GetExtension(input.SourcePath)}");
        image.Write(tempPath);

        var sizeBytes = new FileInfo(tempPath).Length;

        return Task.FromResult(input with
        {
            SourcePath = tempPath,
            WidthPixels = (int)image.Width,
            HeightPixels = (int)image.Height,
            SizeBytes = sizeBytes,
        });
    }

    private static int RequireDimension(int? value, string paramName) =>
        value ?? throw new ArgumentException($"Falta la dimension '{paramName}'.", paramName);

    private static MagickGeometry PercentageGeometry(double percentage)
    {
        var p = new Percentage(percentage);
        return new MagickGeometry(p, p);
    }
}
