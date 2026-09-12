using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickPixelColorReader : IPixelColorReader
{
    public Task<PixelColorResult> ReadAsync(
        ImageAsset input,
        double fractionX,
        double fractionY,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var image = new MagickImage(input.SourcePath);

        var x = Math.Clamp((int)(fractionX * image.Width), 0, (int)image.Width - 1);
        var y = Math.Clamp((int)(fractionY * image.Height), 0, (int)image.Height - 1);

        using var pixels = image.GetPixels();
        var pixel = pixels.GetPixel(x, y).ToArray();
        var quantumMax = (double)Quantum.Max;

        byte To8Bit(double value) => (byte)Math.Round(value / quantumMax * 255);

        var r = To8Bit(pixel[0]);
        var g = To8Bit(pixel[1]);
        var b = To8Bit(pixel[2]);
        var a = image.HasAlpha ? To8Bit(pixel[3]) : (byte)255;

        var hex = $"#{r:X2}{g:X2}{b:X2}";
        return Task.FromResult(new PixelColorResult(hex, r, g, b, a));
    }
}
