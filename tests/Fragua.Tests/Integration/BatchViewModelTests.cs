using Fragua.App.Models;
using Fragua.App.ViewModels;
using Fragua.Core;
using Fragua.Imaging;
using Fragua.Tests.Fixtures;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba el flujo de Lotes end-to-end tal como lo dispara la interfaz
/// (SetBatchFolder + StartBatchCommand), sin depender del dialogo nativo de
/// carpeta ni de automatizar la ventana. Usa Magick.NET real, igual que
/// MagickIntegrationTests.
/// </summary>
public sealed class BatchViewModelTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-batch-{Guid.NewGuid():N}");

    public BatchViewModelTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    private static ConvertViewModel CreateViewModel()
    {
        var loader = new MagickImageAssetLoader();
        var resizer = new MagickImageResizer();
        var writer = new MagickImageAssetWriter();
        var pipeline = new ImagePipeline(loader);
        var modelProvider = new SiluetaModelProvider(new HttpClient());
        return new ConvertViewModel(pipeline, resizer, writer, new NullBackgroundRemover(), modelProvider, new MagickImageVectorizer());
    }

    [Fact]
    public void SetBatchFolder_detecta_los_archivos_de_imagen_e_ignora_el_resto()
    {
        TestImages.CreateSolidPng(200, 200, _workDir);
        TestImages.CreateSolidPng(200, 200, _workDir);
        File.WriteAllText(Path.Combine(_workDir, "notas.txt"), "no es una imagen");

        var vm = CreateViewModel();
        vm.SetBatchFolder(_workDir);

        Assert.Equal(2, vm.BatchFiles.Count);
        Assert.All(vm.BatchFiles, f => Assert.Equal(BatchFileStatus.Pending, f.Status));
    }

    [Fact]
    public async Task StartBatchAsync_convierte_todos_los_archivos_y_los_marca_Done()
    {
        for (var i = 0; i < 4; i++)
        {
            TestImages.CreateSolidPng(300, 200, _workDir);
        }

        var vm = CreateViewModel();
        vm.SetBatchFolder(_workDir);
        vm.ResizeEnabled = false;
        vm.TargetFormat = ImageFormat.WebP;

        await vm.StartBatchCommand.ExecuteAsync(null);

        Assert.Equal(4, vm.BatchCompletedCount);
        Assert.Equal(0, vm.BatchFailedCount);
        Assert.All(vm.BatchFiles, f => Assert.Equal(BatchFileStatus.Done, f.Status));

        var outputDir = Path.Combine(_workDir, "Fragua");
        var outputs = Directory.GetFiles(outputDir, "*.webp");
        Assert.Equal(4, outputs.Length);
    }

    [Fact]
    public async Task StartBatchAsync_respeta_el_ajuste_de_redimension_de_la_pestana_Convertir()
    {
        TestImages.CreateSolidPng(800, 800, _workDir);

        var vm = CreateViewModel();
        vm.SetBatchFolder(_workDir);
        vm.ResizeEnabled = true;
        vm.WidthText = "100";
        vm.HeightText = "100";
        vm.TargetFormat = ImageFormat.Png;

        await vm.StartBatchCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.BatchCompletedCount);

        var outputDir = Path.Combine(_workDir, "Fragua");
        var output = Directory.GetFiles(outputDir, "*.png").Single();
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(output, CancellationToken.None);

        Assert.Equal(100, asset.WidthPixels);
        Assert.Equal(100, asset.HeightPixels);
    }
}
