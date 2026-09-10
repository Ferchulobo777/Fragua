using System.IO.Compression;

namespace Fragua.Imaging;

/// <summary>
/// Real-ESRGAN-General-x4v3 (variante liviana, BSD-3-Clause, publicada por
/// Qualcomm AI Hub). La pesada (x4plus, 23 bloques) tarda ~35 segundos por
/// mosaico de 128px en CPU, impracticable para una foto real; esta version
/// liviana tarda ~2 segundos, medido antes de elegirla, no supuesto.
///
/// El modelo viene en dos archivos (.onnx + .data, formato de datos
/// externos de ONNX): se descargan juntos dentro del zip oficial y se
/// extraen los dos al cache, no alcanza con el .onnx solo.
/// </summary>
public sealed class UpscaleModelProvider
{
    private const string DownloadUrl =
        "https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/real_esrgan_general_x4v3/releases/v0.62.1/real_esrgan_general_x4v3-onnx-float.zip";

    private const long MinExpectedOnnxBytes = 1_000_000;
    private const long MinExpectedDataBytes = 3_000_000;

    private readonly string _modelDirectory;
    private readonly HttpClient _httpClient;

    public UpscaleModelProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _modelDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Fragua", "models", "upscale");
    }

    public string ModelPath => Path.Combine(_modelDirectory, "real_esrgan_general_x4v3.onnx");
    private string DataPath => Path.Combine(_modelDirectory, "real_esrgan_general_x4v3.data");

    public bool IsModelReady =>
        File.Exists(ModelPath) && new FileInfo(ModelPath).Length >= MinExpectedOnnxBytes
        && File.Exists(DataPath) && new FileInfo(DataPath).Length >= MinExpectedDataBytes;

    public async Task DownloadAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_modelDirectory);

        var tempZipPath = Path.Combine(_modelDirectory, "download.zip.tmp");

        using (var response = await _httpClient
            .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;

            await using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await httpStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                readTotal += read;
                if (totalBytes is > 0)
                {
                    // La extraccion es rapida frente a la descarga; el 90%
                    // del progreso reportado es la parte que de verdad tarda.
                    progress?.Report((double)readTotal / totalBytes.Value * 0.9);
                }
            }
        }

        using (var archive = ZipFile.OpenRead(tempZipPath))
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.Name.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(ModelPath, overwrite: true);
                }
                else if (entry.Name.EndsWith(".data", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(DataPath, overwrite: true);
                }
            }
        }

        File.Delete(tempZipPath);
        progress?.Report(1);
    }
}
