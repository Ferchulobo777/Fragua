using Fragua.Core;
using Fragua.Core.Operations;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;

namespace Fragua.Tests.Integration;

/// <summary>
/// Conversiones reales de Magick.NET sobre imagenes de prueba generadas en
/// runtime, verificando formato, dimensiones y que el peso baje cuando
/// corresponde. Nivel "Integracion" del plan de testing.
/// </summary>
public sealed class MagickIntegrationTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-it-{Guid.NewGuid():N}");

    public MagickIntegrationTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task Loader_lee_dimensiones_y_formato_reales()
    {
        var path = TestImages.CreateSolidPng(800, 600, _workDir);
        var loader = new MagickImageAssetLoader();

        var asset = await loader.LoadAsync(path, CancellationToken.None);

        Assert.Equal(800, asset.WidthPixels);
        Assert.Equal(600, asset.HeightPixels);
        Assert.Equal(ImageFormat.Png, asset.Format);
    }

    [Fact]
    public async Task Resizer_Exact_produce_las_dimensiones_pedidas()
    {
        var path = TestImages.CreateSolidPng(800, 600, _workDir);
        var loader = new MagickImageAssetLoader();
        var resizer = new MagickImageResizer();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var resized = await resizer.ResizeAsync(
            asset,
            new ResizeSpec(ResizeMode.Exact, Width: 200, Height: 150),
            CancellationToken.None);

        Assert.Equal(200, resized.WidthPixels);
        Assert.Equal(150, resized.HeightPixels);
    }

    [Fact]
    public async Task Resizer_Fit_mantiene_la_proporcion_original()
    {
        var path = TestImages.CreateSolidPng(800, 400, _workDir); // 2:1
        var loader = new MagickImageAssetLoader();
        var resizer = new MagickImageResizer();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var resized = await resizer.ResizeAsync(
            asset,
            new ResizeSpec(ResizeMode.Fit, Width: 200, Height: 200),
            CancellationToken.None);

        Assert.Equal(200, resized.WidthPixels);
        Assert.Equal(100, resized.HeightPixels); // conserva 2:1
    }

    [Fact]
    public async Task Writer_convierte_a_jpeg_y_el_archivo_final_existe()
    {
        var path = TestImages.CreateSolidPng(400, 400, _workDir);
        var loader = new MagickImageAssetLoader();
        var writer = new MagickImageAssetWriter();
        var asset = await loader.LoadAsync(path, CancellationToken.None);
        var destination = Path.Combine(_workDir, "salida");

        var written = await writer.WriteAsync(asset, destination, ImageFormat.Jpeg, optimize: null, CancellationToken.None);

        Assert.True(File.Exists(written.SourcePath));
        Assert.EndsWith(".jpg", written.SourcePath);
        Assert.Equal(ImageFormat.Jpeg, written.Format);
    }

    [Fact]
    public async Task Writer_convierte_a_webp_y_el_peso_baja_frente_al_bmp_sin_comprimir()
    {
        // BMP no comprime nada: es la linea de base real de "foto tal cual
        // sale de la camara" contra la que tiene sentido medir la mejora.
        var path = TestImages.CreateNoisyBmp(600, 600, _workDir);
        var loader = new MagickImageAssetLoader();
        var writer = new MagickImageAssetWriter();
        var asset = await loader.LoadAsync(path, CancellationToken.None);
        var originalSize = asset.SizeBytes;
        var destination = Path.Combine(_workDir, "salida");

        var written = await writer.WriteAsync(asset, destination, ImageFormat.WebP, optimize: null, CancellationToken.None);

        Assert.True(written.SizeBytes < originalSize,
            $"WebP ({written.SizeBytes} bytes) deberia pesar menos que el BMP original ({originalSize} bytes).");
    }

    [Fact]
    public async Task Writer_optimizar_con_calidad_baja_pesa_menos_que_calidad_alta()
    {
        var path = TestImages.CreateNoisyBmp(600, 600, _workDir);
        var loader = new MagickImageAssetLoader();
        var writer = new MagickImageAssetWriter();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var altaCalidad = await writer.WriteAsync(
            asset, Path.Combine(_workDir, "alta"), ImageFormat.Jpeg,
            new OptimizeSpec(Quality: 95), CancellationToken.None);

        var bajaCalidad = await writer.WriteAsync(
            asset, Path.Combine(_workDir, "baja"), ImageFormat.Jpeg,
            new OptimizeSpec(Quality: 20), CancellationToken.None);

        Assert.True(bajaCalidad.SizeBytes < altaCalidad.SizeBytes,
            $"Calidad 20 ({bajaCalidad.SizeBytes} bytes) deberia pesar menos que calidad 95 ({altaCalidad.SizeBytes} bytes).");
    }

    [Fact]
    public async Task Pipeline_completo_redimensiona_y_convierte_en_una_sola_pasada()
    {
        var path = TestImages.CreateSolidPng(1000, 1000, _workDir);
        var destination = Path.Combine(_workDir, "salida");
        var loader = new MagickImageAssetLoader();
        var resizer = new MagickImageResizer();
        var writer = new MagickImageAssetWriter();

        var pipeline = new ImagePipeline(loader);
        var job = new ImageJob(path, destination,
        [
            new ResizeOperation(resizer, new ResizeSpec(ResizeMode.Exact, Width: 300, Height: 300)),
            new ConvertFormatOperation(writer, destination, ImageFormat.WebP),
        ]);

        var result = await pipeline.RunAsync(job, progress: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(300, result.Output!.WidthPixels);
        Assert.Equal(ImageFormat.WebP, result.Output.Format);
        Assert.True(File.Exists(result.Output.SourcePath));
    }
}
