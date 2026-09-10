using Fragua.Core;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba real de segmentacion: no alcanza con que no tire excepcion, tiene
/// que separar primer plano de fondo de verdad. Se verifica sobre una
/// figura solida en un color distinto al fondo, el caso mas simple posible
/// pero suficiente para probar que el tensor de entrada, la normalizacion
/// min-max de la salida y la aplicacion de la mascara como alfa estan bien
/// conectados de punta a punta.
///
/// Descarga silueta.onnx (~43MB) la primera vez que corre si no esta
/// cacheado en %LOCALAPPDATA%\Fragua\models. Requiere red la primera vez.
/// </summary>
public sealed class BackgroundRemovalTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-bgtest-{Guid.NewGuid():N}");

    public BackgroundRemovalTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task RemoveBackgroundAsync_deja_transparente_el_fondo_y_opaco_el_sujeto()
    {
        var httpClient = new HttpClient();
        var provider = new SiluetaModelProvider(httpClient);
        if (!provider.IsModelReady)
        {
            await provider.DownloadAsync(progress: null, CancellationToken.None);
        }

        var path = TestImages.CreateShapeOnSolidBackground(_workDir);
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        using var remover = new OnnxBackgroundRemover(provider.ModelPath);
        var result = await remover.RemoveBackgroundAsync(asset, CancellationToken.None);

        Assert.Equal(ImageFormat.Png, result.Format);
        Assert.True(File.Exists(result.SourcePath));

        using var output = new ImageMagick.MagickImage(result.SourcePath);
        Assert.True(output.HasAlpha);

        var pixels = output.GetPixels();

        // Esquina: fondo solido, tiene que quedar practicamente transparente.
        var cornerAlpha = pixels.GetPixel(5, 5).ToArray()[3];
        // Centro: adentro de la figura, tiene que quedar practicamente opaco.
        var centerAlpha = pixels.GetPixel((int)output.Width / 2, (int)output.Height / 2).ToArray()[3];

        var quantumMax = ImageMagick.Quantum.Max;
        Assert.True(cornerAlpha < quantumMax * 0.15,
            $"La esquina (fondo) deberia ser casi transparente, alfa={cornerAlpha}");
        Assert.True(centerAlpha > quantumMax * 0.85,
            $"El centro (sujeto) deberia ser casi opaco, alfa={centerAlpha}");
    }
}
