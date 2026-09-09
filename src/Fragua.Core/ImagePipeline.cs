namespace Fragua.Core;

/// <summary>
/// Corre uno o varios <see cref="ImageJob"/> aplicando sus operaciones en
/// orden. Es el corazon del producto: "convertir, redimensionar y optimizar"
/// es una sola pasada por esta clase, no tres botones sueltos.
///
/// Contrato exigido por el plan:
/// - CancellationToken atraviesa todo, un lote de 200 fotos se tiene que
///   poder cortar a mitad de camino.
/// - Un error en un archivo no corta el lote: se registra en su
///   ImageJobResult y el resto sigue.
/// - El progreso reportado es real (archivo y paso actuales), nunca una
///   barra que avanza sola.
/// </summary>
public sealed class ImagePipeline
{
    private readonly IImageAssetLoader _loader;

    public ImagePipeline(IImageAssetLoader loader)
    {
        _loader = loader;
    }

    public async Task<ImageJobResult> RunAsync(
        ImageJob job,
        IProgress<ImageJobProgress>? progress,
        CancellationToken cancellationToken)
    {
        return await RunAsync(job, progress, completedJobs: 0, totalJobs: 1, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Corre un lote completo. Respeta <paramref name="maxDegreeOfParallelism"/>
    /// porque saturar los nucleos con imagenes grandes hace que el sistema
    /// entero se arrastre, no solo la conversion.
    /// </summary>
    public async Task<IReadOnlyList<ImageJobResult>> RunBatchAsync(
        IReadOnlyList<ImageJob> jobs,
        int maxDegreeOfParallelism,
        IProgress<ImageJobProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new ImageJobResult[jobs.Count];
        var completed = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, jobs.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism),
                CancellationToken = cancellationToken,
            },
            async (index, ct) =>
            {
                var completedSoFar = Interlocked.Increment(ref completed) - 1;
                results[index] = await RunAsync(jobs[index], progress, completedSoFar, jobs.Count, ct)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);

        return results;
    }

    private async Task<ImageJobResult> RunAsync(
        ImageJob job,
        IProgress<ImageJobProgress>? progress,
        int completedJobs,
        int totalJobs,
        CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report(new ImageJobProgress(job.SourcePath, "Cargando", completedJobs, totalJobs));
            var asset = await _loader.LoadAsync(job.SourcePath, cancellationToken).ConfigureAwait(false);

            foreach (var operation in job.Operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ImageJobProgress(job.SourcePath, operation.DisplayName, completedJobs, totalJobs));
                asset = await operation.ApplyAsync(asset, cancellationToken).ConfigureAwait(false);
            }

            return ImageJobResult.Success(job, asset);
        }
        catch (OperationCanceledException)
        {
            return ImageJobResult.Cancelled(job);
        }
        catch (Exception ex)
        {
            return ImageJobResult.Failure(job, ex.Message);
        }
    }
}
