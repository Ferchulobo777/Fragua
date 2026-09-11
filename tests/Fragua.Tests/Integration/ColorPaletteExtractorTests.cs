using Fragua.Imaging;
using Fragua.Tests.Fixtures;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba real con Magick.NET: escribe fixtures con proporciones de color
/// conocidas y confirma que la extraccion devuelve esos mismos numeros, no
/// solo "algun color".
/// </summary>
public sealed class ColorPaletteExtractorTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-palette-{Guid.NewGuid():N}");
    private readonly MagickColorPaletteExtractor _extractor = new();

    public ColorPaletteExtractorTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAsync_devuelve_los_porcentajes_reales_de_cada_franja()
    {
        var path = TestImages.CreateThreeColorBands(_workDir);
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var palette = await _extractor.ExtractAsync(asset, colorCount: 6, CancellationToken.None);

        Assert.Equal(3, palette.Count);
        Assert.Equal("#1B3A8F", palette[0].Hex);
        Assert.Equal(50.0, palette[0].Percentage, 1);
        Assert.Equal("#C0392B", palette[1].Hex);
        Assert.Equal(30.0, palette[1].Percentage, 1);
        Assert.Equal("#27AE60", palette[2].Hex);
        Assert.Equal(20.0, palette[2].Percentage, 1);
    }

    [Fact]
    public async Task ExtractAsync_ignora_los_pixeles_transparentes()
    {
        var path = TestImages.CreateHalfTransparentHalfSolid(_workDir);
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var palette = await _extractor.ExtractAsync(asset, colorCount: 6, CancellationToken.None);

        // Sin el filtro de alfa, la mitad transparente saldria como "negro"
        // al ~50%. El unico color real tiene que ser el celeste, cerca del
        // 100% de lo que SI es contenido visible.
        var only = Assert.Single(palette);
        Assert.Equal("#2878C8", only.Hex);
        Assert.True(only.Percentage > 95, $"Deberia ser casi 100%, fue {only.Percentage}");
    }

    [Fact]
    public async Task ExtractAsync_sobre_imagen_enteramente_transparente_no_rompe_y_devuelve_vacio()
    {
        var path = Path.Combine(_workDir, "empty.png");
        using (var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.Transparent, 100, 100))
        {
            image.HasAlpha = true;
            image.Write(path, ImageMagick.MagickFormat.Png);
        }

        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var palette = await _extractor.ExtractAsync(asset, colorCount: 6, CancellationToken.None);

        Assert.Empty(palette);
    }
}
