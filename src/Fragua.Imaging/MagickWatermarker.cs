using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickWatermarker : IWatermarker
{
    // Proporcionales al lado mas chico de la imagen: una marca de agua de
    // tamano fijo en pixeles se ve enorme en un icono y invisible en una
    // foto de 4000px. Con un piso para que no desaparezca en imagenes chicas.
    private const double FontSizeRatio = 0.05;
    private const double PaddingRatio = 0.03;
    private const int MinFontSize = 10;
    private const int MinPadding = 8;

    public Task<ImageAsset> ApplyAsync(ImageAsset input, WatermarkSpec spec, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = new MagickImage(input.SourcePath);

        var shortSide = Math.Min(image.Width, image.Height);
        var fontSize = Math.Max(MinFontSize, (int)(shortSide * FontSizeRatio));
        var padding = Math.Max(MinPadding, (int)(shortSide * PaddingRatio));

        var color = new MagickColor(MagickColors.White)
        {
            A = (ushort)(Quantum.Max * Math.Clamp(spec.Opacity, 0, 1)),
        };

        image.Settings.FillColor = color;
        image.Settings.Font = "Arial";
        image.Settings.FontPointsize = fontSize;

        var gravity = ToGravity(spec.Position);
        var box = spec.Position == WatermarkPosition.Center
            ? new MagickGeometry(0, 0, image.Width, image.Height)
            : new MagickGeometry(
                padding, padding,
                image.Width - (uint)(padding * 2),
                image.Height - (uint)(padding * 2));

        image.Annotate(spec.Text, box, gravity, 0);

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"fragua_{Guid.NewGuid():N}{Path.GetExtension(input.SourcePath)}");
        image.Write(tempPath);

        var sizeBytes = new FileInfo(tempPath).Length;

        return Task.FromResult(input with
        {
            SourcePath = tempPath,
            SizeBytes = sizeBytes,
        });
    }

    private static Gravity ToGravity(WatermarkPosition position) => position switch
    {
        WatermarkPosition.TopLeft => Gravity.Northwest,
        WatermarkPosition.TopRight => Gravity.Northeast,
        WatermarkPosition.BottomLeft => Gravity.Southwest,
        WatermarkPosition.BottomRight => Gravity.Southeast,
        WatermarkPosition.Center => Gravity.Center,
        _ => throw new NotSupportedException($"Posicion no soportada: {position}"),
    };
}
