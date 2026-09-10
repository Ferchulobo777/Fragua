using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Fragua.Imaging;

/// <summary>
/// Super-resolucion 4x local con Real-ESRGAN-General-x4v3 (BSD-3-Clause).
/// El modelo tiene entrada fija de 128x128, asi que una foto real se
/// procesa en mosaicos con solape y se recompone al final. Medido antes de
/// elegir este modelo: la variante completa (x4plus, 23 bloques) tarda
/// ~35 segundos por mosaico en CPU, impracticable; esta liviana tarda
/// ~2 segundos.
///
/// El recompuesto usa un promedio ponderado por pixel (no un Composite
/// "Over" encadenado): dos mosaicos semitransparentes compuestos con Over
/// no dan un 50/50 real, dan un resultado sesgado hacia el que se compuso
/// ultimo. Se detecto viendo lineas de costura visibles en la primera
/// version y confirmando el motivo antes de corregirlo.
/// </summary>
public sealed class OnnxImageUpscaler : IImageUpscaler, IDisposable
{
    private const int TileSize = 128;
    private const int Scale = 4;
    private const int OutputTileSize = TileSize * Scale;
    private const int Overlap = 20;
    private const int Step = TileSize - (2 * Overlap);

    // El solape real entre dos mosaicos consecutivos (en pixeles de
    // salida): es sobre esta distancia que se desvanece cada borde.
    private const int FeatherDistance = (TileSize - Step) * Scale;

    private readonly string _modelPath;
    private InferenceSession? _session;
    private readonly object _sessionLock = new();

    public OnnxImageUpscaler(string modelPath)
    {
        _modelPath = modelPath;
    }

    private InferenceSession Session
    {
        get
        {
            lock (_sessionLock)
            {
                return _session ??= new InferenceSession(_modelPath);
            }
        }
    }

    public Task<ImageAsset> UpscaleAsync(ImageAsset input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var original = new MagickImage(input.SourcePath);
        var originalWidth = (int)original.Width;
        var originalHeight = (int)original.Height;

        var workWidth = Math.Max(originalWidth, TileSize);
        var workHeight = Math.Max(originalHeight, TileSize);
        using var padded = PadToMinimumSize(original, workWidth, workHeight);

        var xStarts = ComputeTileStarts(workWidth, TileSize, Step);
        var yStarts = ComputeTileStarts(workHeight, TileSize, Step);

        var canvasWidth = workWidth * Scale;
        var canvasHeight = workHeight * Scale;

        // Acumulacion ponderada: cada mosaico suma color*peso y peso a la
        // zona que le toca, y al final se divide. Es lo que hace que el
        // solape entre mosaicos vecinos de un promedio real en vez de un
        // corte duro o un sesgo hacia el ultimo compuesto.
        var accumR = new float[canvasHeight, canvasWidth];
        var accumG = new float[canvasHeight, canvasWidth];
        var accumB = new float[canvasHeight, canvasWidth];
        var accumWeight = new float[canvasHeight, canvasWidth];

        for (var yi = 0; yi < yStarts.Count; yi++)
        {
            for (var xi = 0; xi < xStarts.Count; xi++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var x = xStarts[xi];
                var y = yStarts[yi];

                using var tile = ExtractTile(padded, x, y);
                AccumulateTile(
                    tile,
                    accumR, accumG, accumB, accumWeight,
                    originX: x * Scale,
                    originY: y * Scale,
                    isLeftEdge: xi == 0,
                    isRightEdge: xi == xStarts.Count - 1,
                    isTopEdge: yi == 0,
                    isBottomEdge: yi == yStarts.Count - 1);
            }
        }

        using var canvas = BuildImageFromAccumulation(accumR, accumG, accumB, accumWeight, canvasWidth, canvasHeight);

        // El padding para llegar al minimo de un mosaico (o el sobrante de
        // trabajar en mosaicos de tamano fijo) se recorta aca: el resultado
        // final es exactamente 4x el tamano original, ni un pixel de mas.
        canvas.Crop(new MagickGeometry(0, 0, (uint)(originalWidth * Scale), (uint)(originalHeight * Scale)));
        canvas.ResetPage();

        var tempPath = Path.Combine(Path.GetTempPath(), $"fragua_{Guid.NewGuid():N}.png");
        canvas.Write(tempPath, MagickFormat.Png);

        var sizeBytes = new FileInfo(tempPath).Length;

        return Task.FromResult(input with
        {
            SourcePath = tempPath,
            WidthPixels = originalWidth * Scale,
            HeightPixels = originalHeight * Scale,
            SizeBytes = sizeBytes,
        });
    }

    /// <summary>
    /// Posiciones de inicio de cada mosaico sobre un eje: pasos de
    /// <paramref name="step"/> con solape, y el ultimo mosaico se corre
    /// hacia atras (no se agranda el paso) para que siga entrando completo
    /// en la imagen. Sin huecos, sin necesidad de rellenar el borde final.
    /// </summary>
    private static List<int> ComputeTileStarts(int size, int tile, int step)
    {
        var starts = new List<int> { 0 };
        if (size <= tile)
        {
            return starts;
        }

        var pos = 0;
        while (true)
        {
            pos += step;
            if (pos + tile >= size)
            {
                starts.Add(size - tile);
                break;
            }
            starts.Add(pos);
        }

        return starts;
    }

    /// <summary>
    /// Si la imagen es mas chica que un mosaico en algun eje, se rellena
    /// con el color promedio (aproximacion simple; extender el borde de
    /// verdad no vale la complejidad para lo que es un caso raro: fotos ya
    /// mas chicas que 128px de origen).
    /// </summary>
    private static MagickImage PadToMinimumSize(MagickImage source, int minWidth, int minHeight)
    {
        if (source.Width >= minWidth && source.Height >= minHeight)
        {
            return (MagickImage)source.Clone();
        }

        var stats = source.Statistics();
        var avgColor = new MagickColor(
            (ushort)stats.Composite().Mean,
            (ushort)stats.Composite().Mean,
            (ushort)stats.Composite().Mean);

        var padded = (MagickImage)source.Clone();
        padded.BackgroundColor = avgColor;
        padded.Extent((uint)minWidth, (uint)minHeight, Gravity.Northwest, avgColor);
        return padded;
    }

    private static MagickImage ExtractTile(MagickImage source, int x, int y)
    {
        var tile = (MagickImage)source.Clone();
        tile.Crop(new MagickGeometry(x, y, (uint)TileSize, (uint)TileSize));
        tile.ResetPage();
        return tile;
    }

    /// <summary>
    /// Peso 0..1 de un pixel dentro del mosaico segun que tan cerca esta de
    /// un borde que SI tiene un vecino solapado. Los bordes que coinciden
    /// con el borde real de la imagen (isEdge=true de ese lado) no se
    /// desvanecen: no hay nada del otro lado con que promediar.
    /// </summary>
    private static float EdgeWeight(int distanceFromEdge, bool isTrueEdge)
    {
        if (isTrueEdge)
        {
            return 1f;
        }
        return Math.Clamp((distanceFromEdge + 0.5f) / FeatherDistance, 0f, 1f);
    }

    private void AccumulateTile(
        MagickImage tile,
        float[,] accumR, float[,] accumG, float[,] accumB, float[,] accumWeight,
        int originX, int originY,
        bool isLeftEdge, bool isRightEdge, bool isTopEdge, bool isBottomEdge)
    {
        var pixels = tile.GetPixels();
        var max = (float)Quantum.Max;
        var input = new DenseTensor<float>([1, 3, TileSize, TileSize]);

        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                var p = pixels.GetPixel(x, y).ToArray();
                input[0, 0, y, x] = p[0] / max;
                input[0, 1, y, x] = p[1] / max;
                input[0, 2, y, x] = p[2] / max;
            }
        }

        using var results = Session.Run([NamedOnnxValue.CreateFromTensor(Session.InputMetadata.Keys.First(), input)]);
        var output = results[0].AsTensor<float>();

        for (var ty = 0; ty < OutputTileSize; ty++)
        {
            var wY = EdgeWeight(ty, isTopEdge) * EdgeWeight(OutputTileSize - 1 - ty, isBottomEdge);
            var canvasY = originY + ty;

            for (var tx = 0; tx < OutputTileSize; tx++)
            {
                var wX = EdgeWeight(tx, isLeftEdge) * EdgeWeight(OutputTileSize - 1 - tx, isRightEdge);
                var weight = wX * wY;
                if (weight <= 0f)
                {
                    continue;
                }

                var canvasX = originX + tx;
                var r = Math.Clamp(output[0, 0, ty, tx], 0f, 1f) * max;
                var g = Math.Clamp(output[0, 1, ty, tx], 0f, 1f) * max;
                var b = Math.Clamp(output[0, 2, ty, tx], 0f, 1f) * max;

                accumR[canvasY, canvasX] += r * weight;
                accumG[canvasY, canvasX] += g * weight;
                accumB[canvasY, canvasX] += b * weight;
                accumWeight[canvasY, canvasX] += weight;
            }
        }
    }

    private static MagickImage BuildImageFromAccumulation(
        float[,] accumR, float[,] accumG, float[,] accumB, float[,] accumWeight,
        int width, int height)
    {
        var image = new MagickImage(MagickColors.Black, (uint)width, (uint)height);
        var pixels = image.GetPixelsUnsafe();
        var max = (float)Quantum.Max;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var weight = accumWeight[y, x];
                if (weight <= 0f)
                {
                    continue;
                }

                var r = (ushort)Math.Clamp(accumR[y, x] / weight, 0, max);
                var g = (ushort)Math.Clamp(accumG[y, x] / weight, 0, max);
                var b = (ushort)Math.Clamp(accumB[y, x] / weight, 0, max);
                pixels.SetPixel(x, y, [r, g, b]);
            }
        }

        return image;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
