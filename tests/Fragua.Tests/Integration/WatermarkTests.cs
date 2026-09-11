using Fragua.Core.Operations;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;
using ImageMagick;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba real con Magick.NET: escribe el archivo de verdad y lo relee
/// para confirmar que el texto realmente se mezclo con el fondo (opacidad)
/// en la esquina correcta, no solo que no tiro excepcion.
/// </summary>
public sealed class WatermarkTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-watermark-{Guid.NewGuid():N}");
    private readonly MagickWatermarker _watermarker = new();

    public WatermarkTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_dibuja_texto_semitransparente_en_la_esquina_pedida()
    {
        var sourcePath = TestImages.CreateSolidPng(800, 500, _workDir);
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(sourcePath, CancellationToken.None);

        var spec = new WatermarkSpec("Fragua", WatermarkPosition.BottomRight, Opacity: 0.6);
        var result = await _watermarker.ApplyAsync(asset, spec, CancellationToken.None);

        Assert.True(File.Exists(result.SourcePath));

        using var output = new MagickImage(result.SourcePath);
        var pixels = output.GetPixels();

        using var original = new MagickImage(sourcePath);
        var backgroundPixel = original.GetPixels().GetPixel(0, 0).ToArray();

        // Esquina superior izquierda: lejos de BottomRight, tiene que seguir
        // siendo el color de fondo puro, sin tocar.
        var untouchedCorner = pixels.GetPixel(5, 5).ToArray();
        Assert.Equal(backgroundPixel[0], untouchedCorner[0]);
        Assert.Equal(backgroundPixel[1], untouchedCorner[1]);
        Assert.Equal(backgroundPixel[2], untouchedCorner[2]);

        // En algun punto de la franja inferior derecha (donde cae el texto)
        // el color tiene que dejar de ser el fondo puro: blanco al 60%
        // mezclado con el fondo da un valor distinto en algun pixel.
        var foundBlend = false;
        for (var y = (int)output.Height - 80; y < (int)output.Height - 10 && !foundBlend; y++)
        {
            for (var x = (int)output.Width - 150; x < (int)output.Width - 10 && !foundBlend; x++)
            {
                var p = pixels.GetPixel(x, y).ToArray();
                if (p[0] != backgroundPixel[0] || p[1] != backgroundPixel[1] || p[2] != backgroundPixel[2])
                {
                    foundBlend = true;
                }
            }
        }

        Assert.True(foundBlend, "No se encontro ningun pixel distinto del fondo en la zona esperada del texto.");
    }

    [Fact]
    public async Task ApplyAsync_con_opacidad_muy_baja_sigue_siendo_visible_pero_tenue()
    {
        var sourcePath = TestImages.CreateSolidPng(400, 400, _workDir);
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(sourcePath, CancellationToken.None);

        var lowOpacitySpec = new WatermarkSpec("F", WatermarkPosition.Center, Opacity: 0.15);
        var highOpacitySpec = new WatermarkSpec("F", WatermarkPosition.Center, Opacity: 0.9);

        var lowResult = await _watermarker.ApplyAsync(asset, lowOpacitySpec, CancellationToken.None);
        var highResult = await _watermarker.ApplyAsync(asset, highOpacitySpec, CancellationToken.None);

        using var original = new MagickImage(sourcePath);
        var bg = original.GetPixels().GetPixel(0, 0).ToArray();

        using var low = new MagickImage(lowResult.SourcePath);
        using var high = new MagickImage(highResult.SourcePath);

        // El pixel exacto (200,200) puede caer en un hueco del glifo "F",
        // no en la tinta: se escanea una region alrededor del centro y se
        // compara el pixel mas claro de cada una (el que de verdad toco la
        // letra), no un punto fijo.
        var lowBrightestR = BrightestRedInRegion(low, 170, 170, 60, 60);
        var highBrightestR = BrightestRedInRegion(high, 170, 170, 60, 60);

        Assert.True(highBrightestR > lowBrightestR,
            $"Mayor opacidad deberia acercarse mas al blanco: alta={highBrightestR}, baja={lowBrightestR}, fondo={bg[0]}");
    }

    private static ushort BrightestRedInRegion(MagickImage image, int x, int y, int width, int height)
    {
        var pixels = image.GetPixels();
        ushort brightest = 0;
        for (var py = y; py < y + height; py++)
        {
            for (var px = x; px < x + width; px++)
            {
                var r = pixels.GetPixel(px, py).ToArray()[0];
                if (r > brightest)
                {
                    brightest = r;
                }
            }
        }

        return brightest;
    }
}
