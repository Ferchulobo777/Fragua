using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

/// <summary>
/// Encuentra el centro del sujeto con el mismo modelo que Quitar fondo
/// (silueta.onnx), pero sin componer nada: solo calcula un centroide
/// ponderado por intensidad de la mascara. Ponderado, no un bounding box
/// simple, para que un poco de ruido de fondo que el modelo no dejo en
/// negro puro no corra el centro de forma exagerada.
/// </summary>
public sealed class OnnxSubjectDetector : ISubjectDetector, IDisposable
{
    /// <summary>Pixeles de mascara por debajo de esto se ignoran: es fondo, no sujeto.</summary>
    private const double MaskThreshold = 0.15;

    private readonly SiluetaMaskComputer _maskComputer;

    public OnnxSubjectDetector(string modelPath)
    {
        _maskComputer = new SiluetaMaskComputer(modelPath);
    }

    public Task<SubjectCenter> DetectCenterAsync(ImageAsset input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var original = new MagickImage(input.SourcePath);
        using var mask = _maskComputer.ComputeMask(original);

        var pixels = mask.GetPixels();
        var quantumMax = (double)Quantum.Max;

        double sumX = 0, sumY = 0, sumWeight = 0;
        for (var y = 0; y < (int)mask.Height; y++)
        {
            for (var x = 0; x < (int)mask.Width; x++)
            {
                var value = pixels.GetPixel(x, y).ToArray()[0] / quantumMax;
                if (value <= MaskThreshold)
                {
                    continue;
                }

                sumX += x * value;
                sumY += y * value;
                sumWeight += value;
            }
        }

        // Mascara vacia (el modelo no encontro nada por encima del umbral):
        // el centro geometrico es la unica respuesta razonable.
        var centerX = sumWeight > 0 ? sumX / sumWeight : mask.Width / 2.0;
        var centerY = sumWeight > 0 ? sumY / sumWeight : mask.Height / 2.0;

        return Task.FromResult(new SubjectCenter((int)centerX, (int)centerY));
    }

    public void Dispose()
    {
        _maskComputer.Dispose();
    }
}
