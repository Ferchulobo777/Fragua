using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Fragua.Imaging;

/// <summary>
/// La inferencia de silueta.onnx (tensor de entrada, corrida del modelo,
/// mascara de salida normalizada min-max) que antes vivia solo dentro de
/// OnnxBackgroundRemover. Se extrajo aca porque OnnxSubjectDetector necesita
/// exactamente la misma mascara para encontrar el centro del sujeto, y
/// duplicar la inferencia en dos clases es el tipo de cosa que se
/// desincroniza sola con el tiempo.
/// </summary>
internal sealed class SiluetaMaskComputer : IDisposable
{
    private const int ModelInputSize = 320;
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

    private readonly string _modelPath;
    private InferenceSession? _session;
    private readonly object _sessionLock = new();

    public SiluetaMaskComputer(string modelPath)
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

    /// <summary>
    /// Mascara en escala de grises, ya redimensionada al tamano de la
    /// imagen original: blanco donde el modelo cree que esta el sujeto,
    /// negro donde cree que es fondo.
    /// </summary>
    public MagickImage ComputeMask(MagickImage original)
    {
        var tensor = BuildInputTensor(original);

        using var results = Session.Run(
        [
            NamedOnnxValue.CreateFromTensor(Session.InputMetadata.Keys.First(), tensor),
        ]);

        var outputName = Session.OutputMetadata.Keys.First();
        var maskTensor = results.First(r => r.Name == outputName).AsTensor<float>();

        return BuildMaskImage(maskTensor, original.Width, original.Height);
    }

    private static DenseTensor<float> BuildInputTensor(MagickImage original)
    {
        using var resized = (MagickImage)original.Clone();
        resized.Resize(new MagickGeometry((uint)ModelInputSize, (uint)ModelInputSize) { IgnoreAspectRatio = true });
        resized.ColorSpace = ColorSpace.sRGB;

        var pixels = resized.GetPixels();
        var tensor = new DenseTensor<float>([1, 3, ModelInputSize, ModelInputSize]);
        var max = (float)Quantum.Max;

        for (var y = 0; y < ModelInputSize; y++)
        {
            for (var x = 0; x < ModelInputSize; x++)
            {
                var pixel = pixels.GetPixel(x, y).ToArray();
                for (var c = 0; c < 3; c++)
                {
                    var normalized = pixel[c] / max;
                    tensor[0, c, y, x] = (normalized - Mean[c]) / Std[c];
                }
            }
        }

        return tensor;
    }

    /// <summary>
    /// El modelo no aplica sigmoid: el postprocesamiento oficial de
    /// U-2-Net normaliza la salida por min-max antes de usarla como mascara.
    /// </summary>
    private static MagickImage BuildMaskImage(Tensor<float> maskTensor, uint targetWidth, uint targetHeight)
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        for (var y = 0; y < ModelInputSize; y++)
        {
            for (var x = 0; x < ModelInputSize; x++)
            {
                var v = maskTensor[0, 0, y, x];
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }

        var range = Math.Max(max - min, 1e-6f);
        var quantumMax = Quantum.Max;

        using var maskSmall = new MagickImage(MagickColors.Black, (uint)ModelInputSize, (uint)ModelInputSize);
        maskSmall.ColorType = ColorType.Grayscale;
        var maskPixels = maskSmall.GetPixelsUnsafe();

        for (var y = 0; y < ModelInputSize; y++)
        {
            for (var x = 0; x < ModelInputSize; x++)
            {
                var normalized = (maskTensor[0, 0, y, x] - min) / range;
                var quantumValue = (ushort)Math.Clamp(normalized * quantumMax, 0, quantumMax);
                maskPixels.SetPixel(x, y, [quantumValue]);
            }
        }

        var maskFull = (MagickImage)maskSmall.Clone();
        maskFull.Resize(new MagickGeometry(targetWidth, targetHeight) { IgnoreAspectRatio = true });
        return maskFull;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
