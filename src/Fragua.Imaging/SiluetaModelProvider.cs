namespace Fragua.Imaging;

/// <summary>
/// El modelo de segmentacion (silueta.onnx, ~43MB, Apache 2.0) nunca va al
/// repositorio ni al instalador: se baja una sola vez desde el release
/// oficial de rembg y queda cacheado en el perfil del usuario, igual que se
/// resolvio FFmpeg en ForgeMD. Sin esto no hay Quitar fondo, pero el resto
/// de Fragua funciona sin tocar la red ni una vez.
/// </summary>
public sealed class SiluetaModelProvider
{
    private const string DownloadUrl =
        "https://github.com/danielgatis/rembg/releases/download/v0.0.0/silueta.onnx";

    // El archivo real pesa ~43MB; un archivo mucho mas chico en disco es
    // una descarga cortada, no un modelo usable.
    private const long MinExpectedBytes = 30_000_000;

    private readonly string _modelPath;
    private readonly HttpClient _httpClient;

    public SiluetaModelProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        var modelsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Fragua", "models");
        _modelPath = Path.Combine(modelsDir, "silueta.onnx");
    }

    public string ModelPath => _modelPath;

    public bool IsModelReady =>
        File.Exists(_modelPath) && new FileInfo(_modelPath).Length >= MinExpectedBytes;

    public async Task DownloadAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);

        // A un archivo temporal primero: si la descarga se corta a mitad de
        // camino, IsModelReady sigue devolviendo false en vez de un .onnx
        // truncado que despues falla de forma rara adentro de ONNX Runtime.
        var tempPath = _modelPath + ".download";

        using var response = await _httpClient
            .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        await using (var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await httpStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                readTotal += read;
                if (totalBytes is > 0)
                {
                    progress?.Report((double)readTotal / totalBytes.Value);
                }
            }
        }

        File.Move(tempPath, _modelPath, overwrite: true);
        progress?.Report(1);
    }
}
