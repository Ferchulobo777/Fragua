using Fragua.Core;
using Fragua.Tests.Fakes;

namespace Fragua.Tests;

public class ImagePipelineTests
{
    [Fact]
    public async Task RunAsync_aplica_las_operaciones_en_orden()
    {
        var order = new List<string>();
        var first = new FakeOperation("primero", asset =>
        {
            order.Add("primero");
            return asset with { WidthPixels = 400 };
        });
        var second = new FakeOperation("segundo", asset =>
        {
            order.Add("segundo");
            return asset with { HeightPixels = 300 };
        });

        var pipeline = new ImagePipeline(new FakeLoader());
        var job = new ImageJob("foto.png", "salida", [first, second]);

        var result = await pipeline.RunAsync(job, progress: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["primero", "segundo"], order);
        Assert.Equal(400, result.Output!.WidthPixels);
        Assert.Equal(300, result.Output!.HeightPixels);
    }

    [Fact]
    public async Task RunAsync_un_error_no_lanza_excepcion_sino_que_devuelve_Failure()
    {
        var failing = new FakeOperation("falla", throws: new InvalidOperationException("formato invalido"));
        var pipeline = new ImagePipeline(new FakeLoader());
        var job = new ImageJob("foto.png", "salida", [failing]);

        var result = await pipeline.RunAsync(job, progress: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("formato invalido", result.ErrorMessage);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task RunAsync_se_puede_cancelar_a_mitad_de_camino()
    {
        var hangs = new FakeOperation("cuelga", waitForCancellation: true);
        var pipeline = new ImagePipeline(new FakeLoader());
        var job = new ImageJob("foto.png", "salida", [hangs]);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));

        var result = await pipeline.RunAsync(job, progress: null, cts.Token);

        Assert.True(result.WasCancelled);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RunAsync_reporta_progreso_real_con_archivo_y_paso_actual()
    {
        var reports = new List<ImageJobProgress>();
        var progress = new Progress<ImageJobProgress>(reports.Add);
        var op = new FakeOperation("redimensionar");
        var pipeline = new ImagePipeline(new FakeLoader());
        var job = new ImageJob("foto.png", "salida", [op]);

        await pipeline.RunAsync(job, progress, CancellationToken.None);
        await Task.Delay(10); // Progress<T> despacha en el SynchronizationContext capturado.

        Assert.Contains(reports, r => r.SourcePath == "foto.png" && r.CurrentStepName == "redimensionar");
    }

    [Fact]
    public async Task RunBatchAsync_un_archivo_que_falla_no_corta_a_los_demas()
    {
        var ok = new FakeOperation("ok");
        var failing = new FakeOperation("falla", throws: new InvalidOperationException("roto"));
        var pipeline = new ImagePipeline(new FakeLoader());

        var jobs = new[]
        {
            new ImageJob("a.png", "salida", [ok]),
            new ImageJob("b.png", "salida", [failing]),
            new ImageJob("c.png", "salida", [ok]),
        };

        var results = await pipeline.RunBatchAsync(jobs, maxDegreeOfParallelism: 2, progress: null, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(2, results.Count(r => r.Succeeded));
        Assert.Single(results, r => !r.Succeeded && r.ErrorMessage == "roto");
    }

    [Fact]
    public async Task RunBatchAsync_respeta_el_grado_maximo_de_paralelismo()
    {
        var concurrent = 0;
        var maxObserved = 0;
        var gate = new object();

        var op = new FakeOperation("trabaja", asset =>
        {
            lock (gate)
            {
                concurrent++;
                maxObserved = Math.Max(maxObserved, concurrent);
            }
            Thread.Sleep(30);
            lock (gate)
            {
                concurrent--;
            }
            return asset;
        });

        var pipeline = new ImagePipeline(new FakeLoader());
        var jobs = Enumerable.Range(0, 6)
            .Select(i => new ImageJob($"foto{i}.png", "salida", [op]))
            .ToArray();

        await pipeline.RunBatchAsync(jobs, maxDegreeOfParallelism: 2, progress: null, CancellationToken.None);

        Assert.True(maxObserved <= 2, $"Se observaron {maxObserved} trabajos concurrentes, el limite era 2.");
    }

    [Fact]
    public async Task RunBatchAsync_al_cancelar_lanza_OperationCanceledException()
    {
        // A diferencia de RunAsync (que atrapa la cancelacion de un solo
        // trabajo y devuelve WasCancelled), Parallel.ForEachAsync observa el
        // token el mismo y relanza si se cancela el lote entero. Quien llama
        // a RunBatchAsync tiene que envolver la espera en try/catch, no
        // esperar un resultado con items marcados Cancelled.
        var hangs = new FakeOperation("cuelga", waitForCancellation: true);
        var pipeline = new ImagePipeline(new FakeLoader());
        var jobs = Enumerable.Range(0, 4)
            .Select(i => new ImageJob($"foto{i}.png", "salida", [hangs]))
            .ToArray();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.RunBatchAsync(jobs, maxDegreeOfParallelism: 2, progress: null, cts.Token));
    }
}
