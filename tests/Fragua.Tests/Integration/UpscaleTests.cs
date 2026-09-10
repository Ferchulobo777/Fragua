using Fragua.Core;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba real de super-resolucion: descarga el modelo si hace falta y
/// corre inferencia real sobre una imagen mas grande que un mosaico (para
/// ejercitar el ensamblado de varios tiles, no solo uno). Confirma que el
/// resultado mide exactamente 4x el original, no aproximado.
/// </summary>
public sealed class UpscaleTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-upscale-{Guid.NewGuid():N}");

    public UpscaleTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task UpscaleAsync_produce_exactamente_4x_el_tamano_original_sobre_varios_mosaicos()
    {
        var httpClient = new HttpClient();
        var provider = new UpscaleModelProvider(httpClient);
        if (!provider.IsModelReady)
        {
            await provider.DownloadAsync(progress: null, CancellationToken.None);
        }

        // 300x220: mas grande que un mosaico de 128 en los dos ejes, para
        // que el resultado dependa de ensamblar mas de un tile.
        var path = TestImages.CreateShapeOnSolidBackground(_workDir);
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        using var upscaler = new OnnxImageUpscaler(provider.ModelPath);
        var result = await upscaler.UpscaleAsync(asset, CancellationToken.None);

        Assert.Equal(asset.WidthPixels * 4, result.WidthPixels);
        Assert.Equal(asset.HeightPixels * 4, result.HeightPixels);
        Assert.True(File.Exists(result.SourcePath));

        using var output = new ImageMagick.MagickImage(result.SourcePath);
        Assert.Equal((uint)result.WidthPixels, output.Width);
        Assert.Equal((uint)result.HeightPixels, output.Height);
    }
}
