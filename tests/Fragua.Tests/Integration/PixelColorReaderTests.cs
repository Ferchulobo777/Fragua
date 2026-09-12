using Fragua.Imaging;
using Fragua.Tests.Fixtures;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba MagickPixelColorReader (el cuentagotas) con Magick.NET real:
/// confirma que lee el color exacto del punto pedido, no un promedio ni
/// una aproximacion, y que respeta la transparencia real del pixel.
/// </summary>
public sealed class PixelColorReaderTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-eyedropper-{Guid.NewGuid():N}");
    private readonly MagickPixelColorReader _reader = new();
    private readonly MagickImageAssetLoader _loader = new();

    public PixelColorReaderTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsync_devuelve_el_color_exacto_de_cada_franja()
    {
        var path = TestImages.CreateThreeColorBands(_workDir);
        var asset = await _loader.LoadAsync(path, CancellationToken.None);

        var top = await _reader.ReadAsync(asset, 0.5, 0.2, CancellationToken.None);
        var middle = await _reader.ReadAsync(asset, 0.5, 0.65, CancellationToken.None);
        var bottom = await _reader.ReadAsync(asset, 0.5, 0.9, CancellationToken.None);

        Assert.Equal("#1B3A8F", top.Hex);
        Assert.Equal("#C0392B", middle.Hex);
        Assert.Equal("#27AE60", bottom.Hex);
    }

    [Fact]
    public async Task ReadAsync_respeta_la_transparencia_del_pixel()
    {
        var path = TestImages.CreateHalfTransparentHalfSolid(_workDir);
        var asset = await _loader.LoadAsync(path, CancellationToken.None);

        var transparentSide = await _reader.ReadAsync(asset, 0.1, 0.5, CancellationToken.None);
        var solidSide = await _reader.ReadAsync(asset, 0.9, 0.5, CancellationToken.None);

        Assert.Equal((byte)0, transparentSide.Alpha);
        Assert.Equal((byte)255, solidSide.Alpha);
        Assert.Equal("#2878C8", solidSide.Hex);
    }

    [Fact]
    public async Task ReadAsync_con_fraccion_1_no_revienta_y_recorta_al_ultimo_pixel()
    {
        var path = TestImages.CreateThreeColorBands(_workDir);
        var asset = await _loader.LoadAsync(path, CancellationToken.None);

        var result = await _reader.ReadAsync(asset, 1.0, 1.0, CancellationToken.None);

        Assert.Equal("#27AE60", result.Hex);
    }
}
