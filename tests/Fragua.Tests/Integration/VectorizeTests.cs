using Fragua.Core;
using Fragua.Core.Operations;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba real del vectorizado: no alcanza con que produzca un archivo, el
/// SVG resultante tiene que tener estructura real (elementos "path" con
/// datos, no un documento vacio). Usa la misma figura de prueba que
/// BackgroundRemovalTests: colores solidos separables, el caso simple que
/// prueba que el pipeline de cinco pasos esta bien conectado de punta a
/// punta despues del port.
/// </summary>
public sealed class VectorizeTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-vec-{Guid.NewGuid():N}");

    public VectorizeTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    [Fact]
    public async Task VectorizeAsync_produce_un_svg_con_paths_reales()
    {
        var path = TestImages.CreateShapeOnSolidBackground(_workDir);
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var vectorizer = new MagickImageVectorizer();
        var destinationDirectory = Path.Combine(_workDir, "salida");

        var result = await vectorizer.VectorizeAsync(asset, new VectorizeSpec(NumberOfColors: 8), destinationDirectory, CancellationToken.None);

        Assert.Equal(ImageFormat.Svg, result.Format);
        Assert.True(File.Exists(result.SourcePath));
        Assert.EndsWith(".svg", result.SourcePath);

        var svg = await File.ReadAllTextAsync(result.SourcePath);
        Assert.StartsWith("<svg", svg);
        Assert.True(result.SizeBytes > 200, $"El SVG parece vacio o casi vacio ({result.SizeBytes} bytes).");

        // Regresion de un bug real de PaletteGenerator: el espaciado del
        // cubo RGB dividia mal el paso y comprimia toda la paleta en una
        // esquina oscura, asi que el circulo rojo se perdia y todo el SVG
        // quedaba como un solo rectangulo de fondo. Dos colores solidos y
        // separables tienen que producir al menos dos <path> distintos.
        var pathCount = System.Text.RegularExpressions.Regex.Count(svg, "<path");
        Assert.True(pathCount >= 2,
            $"Se esperaban al menos 2 paths (fondo + figura), se encontraron {pathCount}. La paleta puede estar colapsando los colores.");
    }

    [Fact]
    public async Task VectorizeOperation_se_integra_al_pipeline_como_paso_terminal()
    {
        var path = TestImages.CreateShapeOnSolidBackground(_workDir);
        var loader = new MagickImageAssetLoader();
        var pipeline = new ImagePipeline(loader);
        var destinationDirectory = Path.Combine(_workDir, "salida");

        var job = new ImageJob(path, destinationDirectory,
        [
            new VectorizeOperation(new MagickImageVectorizer(), new VectorizeSpec(), destinationDirectory),
        ]);

        var result = await pipeline.RunAsync(job, progress: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ImageFormat.Svg, result.Output!.Format);
        Assert.True(File.Exists(result.Output.SourcePath));
    }
}
