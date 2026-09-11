using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickIconSetGenerator : IIconSetGenerator
{
    /// <summary>Windows no acepta frames mas grandes que esto dentro de un .ico.</summary>
    private const int MaxIcoFrameSize = 256;

    public Task<IconSetResult> GenerateAsync(
        ImageAsset input,
        IconSetSpec spec,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(destinationDirectory);

        var baseName = Path.GetFileNameWithoutExtension(input.SourcePath);
        var generated = new List<string>();

        using var source = new MagickImage(input.SourcePath);

        // Forzar cuadrado: lienzo transparente del lado mas largo, con la
        // imagen centrada. La mayoria de las fuentes reales (un logo, una
        // foto) no son cuadradas, y un icono si tiene que serlo.
        var side = Math.Max(source.Width, source.Height);
        using var square = new MagickImage(MagickColors.Transparent, side, side);
        square.Composite(source, Gravity.Center, CompositeOperator.Over);

        using var icoCollection = spec.IncludeIco ? new MagickImageCollection() : null;

        foreach (var size in spec.Sizes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var frame = (MagickImage)square.Clone();
            frame.Resize((uint)size, (uint)size);

            var pngPath = Path.Combine(destinationDirectory, $"{baseName}-{size}.png");
            frame.Write(pngPath);
            generated.Add(pngPath);

            if (icoCollection is not null && size <= MaxIcoFrameSize)
            {
                icoCollection.Add(frame.Clone());
            }
        }

        if (icoCollection is not null && icoCollection.Count > 0)
        {
            var icoPath = Path.Combine(destinationDirectory, $"{baseName}.ico");
            icoCollection.Write(icoPath);
            generated.Add(icoPath);
        }

        return Task.FromResult(new IconSetResult(generated));
    }
}
