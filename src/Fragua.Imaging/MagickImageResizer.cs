using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickImageResizer : IImageResizer
{
    private readonly ISubjectDetector _subjectDetector;

    public MagickImageResizer(ISubjectDetector subjectDetector)
    {
        _subjectDetector = subjectDetector;
    }

    public async Task<ImageAsset> ResizeAsync(ImageAsset input, ResizeSpec spec, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = new MagickImage(input.SourcePath);

        if (spec.Mode == ResizeMode.SmartCrop)
        {
            var targetWidth = RequireDimension(spec.Width, nameof(spec.Width));
            var targetHeight = RequireDimension(spec.Height, nameof(spec.Height));
            var center = await _subjectDetector.DetectCenterAsync(input, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            image.Crop(SmartCropWindow(image.Width, image.Height, targetWidth, targetHeight, center));
            image.ResetPage();
            image.Resize(new MagickGeometry((uint)targetWidth, (uint)targetHeight) { IgnoreAspectRatio = true });
        }
        else
        {
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
        }

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"fragua_{Guid.NewGuid():N}{Path.GetExtension(input.SourcePath)}");
        image.Write(tempPath);

        var sizeBytes = new FileInfo(tempPath).Length;

        return input with
        {
            SourcePath = tempPath,
            WidthPixels = (int)image.Width,
            HeightPixels = (int)image.Height,
            SizeBytes = sizeBytes,
        };
    }

    /// <summary>
    /// La ventana de recorte previa al resize final: el rectangulo mas
    /// grande posible con la proporcion del destino que entra en la imagen
    /// original, centrado en el sujeto en vez del centro geometrico, y
    /// recortado (clamp) para no salirse de los bordes.
    /// </summary>
    public static MagickGeometry SmartCropWindow(
        uint originalWidth, uint originalHeight, int targetWidth, int targetHeight, SubjectCenter center)
    {
        var targetAspect = (double)targetWidth / targetHeight;
        var originalAspect = (double)originalWidth / originalHeight;

        int cropWidth, cropHeight;
        if (originalAspect > targetAspect)
        {
            // Original mas ancha que el destino: la altura completa entra,
            // el ancho se recorta.
            cropHeight = (int)originalHeight;
            cropWidth = (int)Math.Round(cropHeight * targetAspect);
        }
        else
        {
            cropWidth = (int)originalWidth;
            cropHeight = (int)Math.Round(cropWidth / targetAspect);
        }

        cropWidth = Math.Min(cropWidth, (int)originalWidth);
        cropHeight = Math.Min(cropHeight, (int)originalHeight);

        var x = Math.Clamp(center.X - cropWidth / 2, 0, (int)originalWidth - cropWidth);
        var y = Math.Clamp(center.Y - cropHeight / 2, 0, (int)originalHeight - cropHeight);

        return new MagickGeometry(x, y, (uint)cropWidth, (uint)cropHeight);
    }

    private static int RequireDimension(int? value, string paramName) =>
        value ?? throw new ArgumentException($"Falta la dimension '{paramName}'.", paramName);

    private static MagickGeometry PercentageGeometry(double percentage)
    {
        var p = new Percentage(percentage);
        return new MagickGeometry(p, p);
    }
}
