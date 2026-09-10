using Fragua.Core;
using Fragua.Core.Operations;
using Fragua.Imaging.ImageTrace;
using Fragua.Imaging.ImageTrace.OptionTypes;
using Fragua.Imaging.ImageTrace.Palettes;
using Fragua.Imaging.ImageTrace.Svg;
using Fragua.Imaging.ImageTrace.Vectorization;
using Fragua.Imaging.ImageTrace.Vectorization.TraceTypes;
using ImageMagick;

namespace Fragua.Imaging;

/// <summary>
/// Orquestacion propia equivalente a ImageTracer.ImageToSvg del proyecto
/// original (MiYanni/ImageTracer.NET, The Unlicense), pero cargando la
/// imagen con Magick.NET en vez de System.Drawing.Bitmap y generando la
/// paleta con PaletteGenerator en vez del Halftone256 de WPF. El pipeline
/// de cinco pasos (cuantizacion, capas, pathscan, interpolacion,
/// segmentacion) es el mismo sin tocar, vendorizado en Fragua.Imaging.ImageTrace.
/// </summary>
public sealed class MagickImageVectorizer : IImageVectorizer
{
    public Task<ImageAsset> VectorizeAsync(
        ImageAsset input,
        VectorizeSpec spec,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(destinationDirectory);

        using var image = new MagickImage(input.SourcePath);
        var width = (int)image.Width;
        var height = (int)image.Height;

        var palette = PaletteGenerator.GeneratePalette(Math.Max(spec.NumberOfColors, 8));
        var colors = ExtractColorReferences(image, width, height);

        var tracing = new Tracing();
        var rendering = new SvgRendering();

        // 1. Cuantizacion de color
        var colorGroups = ColorGrouping.Convert(colors, width, height, palette);
        cancellationToken.ThrowIfCancellationRequested();

        // 2. Separacion en capas y deteccion de bordes
        var rawLayers = Layering.Convert(colorGroups, width, height, palette);
        cancellationToken.ThrowIfCancellationRequested();

        // 3. Pathscan por lote
        var pathPointLayers = rawLayers.ToDictionary(cl => cl.Key, cl => new Layer<PathPointPath>
        {
            Paths = Pathing.Scan(cl.Value, tracing.PathOmit).ToList(),
        });
        cancellationToken.ThrowIfCancellationRequested();

        // 4. Interpolacion por lote
        var interpolationPointLayers = pathPointLayers.ToDictionary(cp => cp.Key, cp => Interpolation.Convert(cp.Value));
        cancellationToken.ThrowIfCancellationRequested();

        // 5. Trazado por lote
        var sequenceLayers = interpolationPointLayers.ToDictionary(ci => ci.Key, ci => new Layer<SequencePath>
        {
            Paths = ci.Value.Paths.Select(path => new SequencePath
            {
                Path = path,
                Sequences = Sequencing.Create(path.Points.Select(p => p.Direction).ToList()).ToList(),
            }).ToList(),
        });

        var segmentLayers = sequenceLayers.ToDictionary(ci => ci.Key, ci => new Layer<SegmentPath>
        {
            Paths = ci.Value.Paths.Select(path => new SegmentPath
            {
                Segments = path.Sequences.Select(s => Segmentation.Fit(path.Path.Points, s, tracing, rendering)).SelectMany(s => s).ToList(),
            }).ToList(),
        });

        var tracedImage = new TracedImage(segmentLayers, width, height);
        var svg = tracedImage.ToSvgString(rendering);

        var fileName = Path.GetFileNameWithoutExtension(input.SourcePath) + ".svg";
        var destinationPath = Path.Combine(destinationDirectory, fileName);
        File.WriteAllText(destinationPath, svg);

        var sizeBytes = new FileInfo(destinationPath).Length;

        return Task.FromResult(input with
        {
            SourcePath = destinationPath,
            Format = ImageFormat.Svg,
            SizeBytes = sizeBytes,
        });
    }

    private static List<ColorReference> ExtractColorReferences(MagickImage image, int width, int height)
    {
        var pixels = image.GetPixels();
        var hasAlpha = image.HasAlpha;
        var colors = new List<ColorReference>(width * height);
        var max = (float)Quantum.Max;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = pixels.GetPixel(x, y).ToArray();
                var r = (byte)(pixel[0] / max * 255);
                var g = (byte)(pixel[1] / max * 255);
                var b = (byte)(pixel[2] / max * 255);
                var a = hasAlpha ? (byte)(pixel[3] / max * 255) : (byte)255;
                colors.Add(new ColorReference(a, r, g, b));
            }
        }

        return colors;
    }
}
