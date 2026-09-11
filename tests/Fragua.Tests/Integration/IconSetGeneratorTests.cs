using Fragua.Core.Operations;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;
using ImageMagick;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba MagickIconSetGenerator con Magick.NET real: escribe archivos de
/// verdad en disco y los relee con Magick.NET para confirmar tamanos
/// exactos, no solo que "no tiro excepcion".
/// </summary>
public sealed class IconSetGeneratorTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-icons-{Guid.NewGuid():N}");
    private readonly MagickIconSetGenerator _generator = new();
    private readonly MagickImageAssetLoader _loader = new();

    public IconSetGeneratorTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAsync_con_fuente_cuadrada_produce_un_png_por_tamano_y_un_ico()
    {
        var sourcePath = TestImages.CreateSolidPng(512, 512, _workDir);
        var asset = await _loader.LoadAsync(sourcePath, CancellationToken.None);
        var spec = new IconSetSpec(Sizes: [16, 32, 256], IncludeIco: true);

        var result = await _generator.GenerateAsync(asset, spec, _workDir, CancellationToken.None);

        Assert.Equal(4, result.GeneratedFiles.Count); // 3 png + 1 ico

        foreach (var size in new[] { 16, 32, 256 })
        {
            var pngPath = result.GeneratedFiles.Single(f => f.EndsWith($"-{size}.png"));
            using var image = new MagickImage(pngPath);
            Assert.Equal((uint)size, image.Width);
            Assert.Equal((uint)size, image.Height);
        }

        var icoPath = result.GeneratedFiles.Single(f => f.EndsWith(".ico"));
        using var icoFrames = new MagickImageCollection(icoPath);
        Assert.Equal(3, icoFrames.Count);
        Assert.Contains(icoFrames, f => f.Width == 16 && f.Height == 16);
        Assert.Contains(icoFrames, f => f.Width == 32 && f.Height == 32);
        Assert.Contains(icoFrames, f => f.Width == 256 && f.Height == 256);
    }

    [Fact]
    public async Task GenerateAsync_con_fuente_no_cuadrada_fuerza_cada_salida_a_cuadrado()
    {
        var sourcePath = TestImages.CreateSolidPng(800, 400, _workDir);
        var asset = await _loader.LoadAsync(sourcePath, CancellationToken.None);
        var spec = new IconSetSpec(Sizes: [64], IncludeIco: false);

        var result = await _generator.GenerateAsync(asset, spec, _workDir, CancellationToken.None);

        var pngPath = Assert.Single(result.GeneratedFiles);
        using var image = new MagickImage(pngPath);
        Assert.Equal(64u, image.Width);
        Assert.Equal(64u, image.Height);
    }

    [Fact]
    public async Task GenerateAsync_512_no_entra_al_ico_pero_si_se_genera_como_png()
    {
        var sourcePath = TestImages.CreateSolidPng(512, 512, _workDir);
        var asset = await _loader.LoadAsync(sourcePath, CancellationToken.None);
        var spec = new IconSetSpec(Sizes: [256, 512], IncludeIco: true);

        var result = await _generator.GenerateAsync(asset, spec, _workDir, CancellationToken.None);

        Assert.Contains(result.GeneratedFiles, f => f.EndsWith("-512.png"));

        var icoPath = result.GeneratedFiles.Single(f => f.EndsWith(".ico"));
        using var icoFrames = new MagickImageCollection(icoPath);
        Assert.Single(icoFrames);
        Assert.Equal(256u, icoFrames[0].Width);
    }

    [Fact]
    public async Task GenerateAsync_sin_IncludeIco_no_escribe_archivo_ico()
    {
        var sourcePath = TestImages.CreateSolidPng(128, 128, _workDir);
        var asset = await _loader.LoadAsync(sourcePath, CancellationToken.None);
        var spec = new IconSetSpec(Sizes: [16, 32], IncludeIco: false);

        var result = await _generator.GenerateAsync(asset, spec, _workDir, CancellationToken.None);

        Assert.Equal(2, result.GeneratedFiles.Count);
        Assert.DoesNotContain(result.GeneratedFiles, f => f.EndsWith(".ico"));
        Assert.False(File.Exists(Path.Combine(_workDir, $"{Path.GetFileNameWithoutExtension(sourcePath)}.ico")));
    }
}
