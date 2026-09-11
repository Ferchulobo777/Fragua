using Fragua.Core;
using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

/// <summary>
/// Segmentacion local con silueta.onnx (variante comprimida de U2Net,
/// mismo contrato de entrada/salida de toda la familia). La inferencia en
/// si vive en SiluetaMaskComputer, compartida con OnnxSubjectDetector.
/// </summary>
public sealed class OnnxBackgroundRemover : IBackgroundRemover, IDisposable
{
    private readonly SiluetaMaskComputer _maskComputer;

    public OnnxBackgroundRemover(string modelPath)
    {
        _maskComputer = new SiluetaMaskComputer(modelPath);
    }

    public Task<ImageAsset> RemoveBackgroundAsync(ImageAsset input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var original = new MagickImage(input.SourcePath);

        using var maskImage = _maskComputer.ComputeMask(original);
        // Suavizado de borde: solo hace falta para el compuesto de alpha,
        // no para encontrar el centro del sujeto (OnnxSubjectDetector usa
        // la mascara cruda).
        maskImage.Blur(0, 1.2);

        original.HasAlpha = true;
        original.Composite(maskImage, CompositeOperator.CopyAlpha);

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"fragua_{Guid.NewGuid():N}.png");
        original.Write(tempPath, MagickFormat.Png);

        var sizeBytes = new FileInfo(tempPath).Length;

        return Task.FromResult(input with
        {
            SourcePath = tempPath,
            Format = ImageFormat.Png,
            SizeBytes = sizeBytes,
        });
    }

    public void Dispose()
    {
        _maskComputer.Dispose();
    }
}
