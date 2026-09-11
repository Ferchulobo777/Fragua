using Fragua.Core.Operations;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;
using ImageMagick;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba MagickCollageComposer con Magick.NET real: confirma que la
/// grilla sale del tamano exacto esperado (columnas x filas, celda +
/// espaciado) y que el recorte "llenar celda" no deja huecos ni distorsiona.
/// </summary>
public sealed class CollageComposerTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-collage-{Guid.NewGuid():N}");
    private readonly MagickCollageComposer _composer = new();

    public CollageComposerTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task ComposeAsync_con_recorte_produce_una_grilla_exacta_sin_huecos()
    {
        var sources = new[]
        {
            TestImages.CreateSolidPng(300, 200, _workDir),
            TestImages.CreateSolidPng(150, 150, _workDir),
            TestImages.CreateSolidPng(400, 300, _workDir),
            TestImages.CreateSolidPng(200, 400, _workDir),
            TestImages.CreateSolidPng(250, 250, _workDir),
        };
        var spec = new CollageSpec(sources, Columns: 3, CellSize: 200, SpacingPixels: 0,
            CropToFill: true, TransparentBackground: true);

        var result = await _composer.ComposeAsync(spec, _workDir, CancellationToken.None);

        // 5 imagenes a 3 columnas: 2 filas. Sin espaciado, la grilla sale
        // exacta a columnas*celda x filas*celda, sin sobrante ni hueco.
        Assert.Equal(600u, result.Width);
        Assert.Equal(400u, result.Height);
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task ComposeAsync_respeta_el_espaciado_entre_celdas()
    {
        var sources = new[]
        {
            TestImages.CreateSolidPng(200, 200, _workDir),
            TestImages.CreateSolidPng(200, 200, _workDir),
            TestImages.CreateSolidPng(200, 200, _workDir),
            TestImages.CreateSolidPng(200, 200, _workDir),
        };
        var spec = new CollageSpec(sources, Columns: 2, CellSize: 200, SpacingPixels: 12,
            CropToFill: true, TransparentBackground: true);

        var result = await _composer.ComposeAsync(spec, _workDir, CancellationToken.None);

        // 2 columnas x 2 filas, cada celda con 12px de margen a cada lado.
        Assert.Equal((200u + 24u) * 2, result.Width);
        Assert.Equal((200u + 24u) * 2, result.Height);
    }

    [Fact]
    public async Task ComposeAsync_sin_recorte_muestra_la_imagen_completa_sin_distorsion()
    {
        var sources = new[]
        {
            TestImages.CreateSolidPng(200, 400, _workDir),
            TestImages.CreateSolidPng(400, 200, _workDir),
        };
        var spec = new CollageSpec(sources, Columns: 2, CellSize: 200, SpacingPixels: 0,
            CropToFill: false, TransparentBackground: true);

        var result = await _composer.ComposeAsync(spec, _workDir, CancellationToken.None);

        using var output = new MagickImage(result.OutputPath);
        using var pixels = output.GetPixels();
        var quantumMax = (double)Quantum.Max;

        // La primera imagen es angosta y alta (200x400): al no recortar,
        // dentro de una celda de 200x200 queda mas chica y centrada, dejando
        // transparente el resto de la celda (por ejemplo, la esquina).
        var corner = pixels.GetPixel(2, 2).ToArray();
        Assert.True(corner[3] / quantumMax < 0.05, "La esquina de una celda sin recorte deberia quedar transparente.");
    }

    [Fact]
    public async Task ComposeAsync_sin_fondo_transparente_usa_blanco()
    {
        var sources = new[]
        {
            TestImages.CreateSolidPng(200, 400, _workDir),
            TestImages.CreateSolidPng(200, 200, _workDir),
        };
        var spec = new CollageSpec(sources, Columns: 2, CellSize: 200, SpacingPixels: 0,
            CropToFill: false, TransparentBackground: false);

        var result = await _composer.ComposeAsync(spec, _workDir, CancellationToken.None);

        using var output = new MagickImage(result.OutputPath);
        using var pixels = output.GetPixels();
        var corner = pixels.GetPixel(2, 2).ToArray();
        var quantumMax = (double)Quantum.Max;

        Assert.True(corner[0] / quantumMax > 0.95 && corner[1] / quantumMax > 0.95 && corner[2] / quantumMax > 0.95,
            "El fondo sin transparencia deberia salir blanco.");
    }

    [Fact]
    public async Task ComposeAsync_sin_imagenes_tira_ArgumentException()
    {
        var spec = new CollageSpec([], Columns: 3, CellSize: 200, SpacingPixels: 0,
            CropToFill: true, TransparentBackground: true);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _composer.ComposeAsync(spec, _workDir, CancellationToken.None));
    }

    [Fact]
    public async Task ComposeAsync_llamado_dos_veces_no_pisa_el_archivo_anterior()
    {
        var sources = new[]
        {
            TestImages.CreateSolidPng(100, 100, _workDir),
            TestImages.CreateSolidPng(100, 100, _workDir),
        };
        var spec = new CollageSpec(sources, Columns: 2, CellSize: 100, SpacingPixels: 0,
            CropToFill: true, TransparentBackground: true);

        var first = await _composer.ComposeAsync(spec, _workDir, CancellationToken.None);
        var second = await _composer.ComposeAsync(spec, _workDir, CancellationToken.None);

        Assert.NotEqual(first.OutputPath, second.OutputPath);
        Assert.True(File.Exists(first.OutputPath));
        Assert.True(File.Exists(second.OutputPath));
    }
}
