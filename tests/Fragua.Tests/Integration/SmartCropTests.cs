using Fragua.Core.Operations;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba real de extremo a extremo: deteccion del sujeto con el modelo
/// real (silueta.onnx) mas el recorte geometrico, sobre una fuente ancha
/// con el sujeto corrido a un costado. Si el centro se detecta mal o la
/// ventana de recorte no lo sigue, el resultado sale con el fondo en el
/// centro en vez del sujeto.
///
/// Descarga silueta.onnx (~43MB) la primera vez que corre si no esta
/// cacheado en %LOCALAPPDATA%\Fragua\models. Requiere red la primera vez.
/// </summary>
public sealed class SmartCropTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-smartcrop-{Guid.NewGuid():N}");

    public SmartCropTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResizeAsync_SmartCrop_sigue_al_sujeto_descentrado()
    {
        var httpClient = new HttpClient();
        var provider = new SiluetaModelProvider(httpClient);
        if (!provider.IsModelReady)
        {
            await provider.DownloadAsync(progress: null, CancellationToken.None);
        }

        var path = TestImages.CreateOffCenterShape(_workDir);
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        using var detector = new OnnxSubjectDetector(provider.ModelPath);
        var resizer = new MagickImageResizer(detector);

        var spec = new ResizeSpec(ResizeMode.SmartCrop, Width: 200, Height: 200);
        var result = await resizer.ResizeAsync(asset, spec, CancellationToken.None);

        Assert.Equal(200, result.WidthPixels);
        Assert.Equal(200, result.HeightPixels);

        using var output = new ImageMagick.MagickImage(result.SourcePath);
        var pixels = output.GetPixels();
        var centerPixel = pixels.GetPixel(100, 100).ToArray();

        // El sujeto es #1B3A6B (azul oscuro), el fondo #EDEDED (gris muy
        // claro). Si el recorte de verdad siguio al sujeto, el centro del
        // resultado tiene que ser oscuro, no el gris de fondo.
        var quantumMax = ImageMagick.Quantum.Max;
        var brightness = (centerPixel[0] + centerPixel[1] + centerPixel[2]) / 3.0 / quantumMax;
        Assert.True(brightness < 0.5,
            $"El centro del recorte deberia caer sobre el sujeto oscuro, brillo normalizado={brightness:F2}");
    }
}
