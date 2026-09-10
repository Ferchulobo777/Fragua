using System.Collections.ObjectModel;
using System.Threading.Channels;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fragua.App.Models;
using Fragua.Core;
using Fragua.Core.Operations;
using Fragua.Imaging;

namespace Fragua.App.ViewModels;

/// <summary>
/// Fase 0 del plan: convertir y redimensionar, en una sola pasada. Nada de
/// lotes, presets ni historial todavia (eso es fase 1).
/// </summary>
public sealed partial class ConvertViewModel : ViewModelBase, IDisposable
{
    private readonly ImagePipeline _pipeline;
    private readonly IImageResizer _resizer;
    private readonly IImageAssetWriter _writer;
    private readonly IBackgroundRemover _backgroundRemover;
    private readonly SiluetaModelProvider _modelProvider;
    private readonly IImageVectorizer _vectorizer;
    private readonly IImageUpscaler _upscaler;
    private readonly UpscaleModelProvider _upscaleModelProvider;

    public ConvertViewModel(
        ImagePipeline pipeline,
        IImageResizer resizer,
        IImageAssetWriter writer,
        IBackgroundRemover backgroundRemover,
        SiluetaModelProvider modelProvider,
        IImageVectorizer vectorizer,
        IImageUpscaler upscaler,
        UpscaleModelProvider upscaleModelProvider)
    {
        _pipeline = pipeline;
        _resizer = resizer;
        _writer = writer;
        _backgroundRemover = backgroundRemover;
        _vectorizer = vectorizer;
        _modelProvider = modelProvider;
        _upscaler = upscaler;
        _upscaleModelProvider = upscaleModelProvider;
    }

    [ObservableProperty]
    private string? _sourcePath;

    [ObservableProperty]
    private ImageAsset? _sourceAsset;

    [ObservableProperty]
    private ImageAsset? _resultAsset;

    [ObservableProperty]
    private Bitmap? _sourcePreview;

    [ObservableProperty]
    private Bitmap? _resultPreview;

    public bool HasSourcePreview => SourcePreview is not null;
    public bool HasResultPreview => ResultPreview is not null;

    partial void OnSourcePreviewChanged(Bitmap? value) => OnPropertyChanged(nameof(HasSourcePreview));
    partial void OnResultPreviewChanged(Bitmap? value) => OnPropertyChanged(nameof(HasResultPreview));

    /// <summary>
    /// Decodifica una miniatura para mostrar en pantalla. Puede fallar en
    /// formatos que Magick.NET lee pero el decodificador de bitmaps de
    /// Avalonia no soporta (algunos TIFF, AVIF segun plataforma); en ese
    /// caso la vista se queda con el texto de la ruta, no rompe nada.
    /// </summary>
    private static Bitmap? TryLoadPreview(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    [ObservableProperty]
    private bool _resizeEnabled = true;

    [ObservableProperty]
    private ResizeMode _resizeMode = ResizeMode.Fit;

    [ObservableProperty]
    private string _widthText = "";

    [ObservableProperty]
    private string _heightText = "";

    [ObservableProperty]
    private ImageFormat _targetFormat = ImageFormat.WebP;

    [ObservableProperty]
    private bool _optimizeEnabled = true;

    [ObservableProperty]
    private int _optimizeQuality = 82;

    private OptimizeSpec? BuildOptimizeSpec() => OptimizeEnabled ? new OptimizeSpec(OptimizeQuality) : null;

    // --- Quitar fondo: fase 2 del plan, IA local (silueta.onnx, ONNX
    // Runtime). El modelo pesa ~43MB y no va en el instalador: se baja una
    // sola vez la primera vez que se activa el checkbox, y queda cacheado
    // para siempre en el perfil del usuario. ---

    [ObservableProperty]
    private bool _removeBackgroundEnabled;

    [ObservableProperty]
    private bool _isDownloadingModel;

    [ObservableProperty]
    private double _modelDownloadProgress;

    [ObservableProperty]
    private string? _modelDownloadError;

    public bool IsModelReady => _modelProvider.IsModelReady;

    async partial void OnRemoveBackgroundEnabledChanged(bool value)
    {
        if (!value || IsModelReady || IsDownloadingModel)
        {
            return;
        }

        IsDownloadingModel = true;
        ModelDownloadProgress = 0;
        ModelDownloadError = null;

        try
        {
            var progress = new Progress<double>(p => ModelDownloadProgress = p);
            await _modelProvider.DownloadAsync(progress, CancellationToken.None);
            OnPropertyChanged(nameof(IsModelReady));
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            // Sin red o el servidor no respondio: se avisa y se destilda el
            // checkbox, no se deja la interfaz esperando algo que no va a
            // llegar.
            ModelDownloadError = "No se pudo descargar el modelo. Revisa la conexion e intenta de nuevo.";
            RemoveBackgroundEnabled = false;
        }
        finally
        {
            IsDownloadingModel = false;
        }
    }

    // --- Vectorizar: fase 3 del plan. Para logos y arte plano, no fotos
    // (ver anti-alcance). Es terminal como Convertir formato: cuando esta
    // activo reemplaza al resto de la salida raster, no se le suma. ---

    [ObservableProperty]
    private bool _vectorizeEnabled;

    [ObservableProperty]
    private int _vectorizeColors = 16;

    // --- Mejorar calidad: super-resolucion 4x local (Real-ESRGAN, ONNX).
    // Mismo patron de descarga que Quitar fondo: primera vez baja el
    // modelo, despues queda cacheado. Puede tardar en fotos grandes (corre
    // en mosaicos de 128px, en CPU), asi que el aviso en la interfaz es
    // honesto sobre eso. ---

    [ObservableProperty]
    private bool _upscaleEnabled;

    [ObservableProperty]
    private bool _isDownloadingUpscaleModel;

    [ObservableProperty]
    private double _upscaleModelDownloadProgress;

    [ObservableProperty]
    private string? _upscaleModelDownloadError;

    public bool IsUpscaleModelReady => _upscaleModelProvider.IsModelReady;

    async partial void OnUpscaleEnabledChanged(bool value)
    {
        if (!value || IsUpscaleModelReady || IsDownloadingUpscaleModel)
        {
            return;
        }

        IsDownloadingUpscaleModel = true;
        UpscaleModelDownloadProgress = 0;
        UpscaleModelDownloadError = null;

        try
        {
            var progress = new Progress<double>(p => UpscaleModelDownloadProgress = p);
            await _upscaleModelProvider.DownloadAsync(progress, CancellationToken.None);
            OnPropertyChanged(nameof(IsUpscaleModelReady));
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            UpscaleModelDownloadError = "No se pudo descargar el modelo. Revisa la conexion e intenta de nuevo.";
            UpscaleEnabled = false;
        }
        finally
        {
            IsDownloadingUpscaleModel = false;
        }
    }

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private double _progressFraction;

    [ObservableProperty]
    private string _progressStepName = "";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _lastRunFailed;

    public ObservableCollection<ImageFormat> AvailableFormats { get; } =
        new(Enum.GetValues<ImageFormat>());

    public ObservableCollection<ResizeMode> AvailableResizeModes { get; } =
        new(Enum.GetValues<ResizeMode>());

    public bool HasSource => SourceAsset is not null;
    public bool HasResult => ResultAsset is not null;
    public bool IsPercentageMode => ResizeMode == ResizeMode.Percentage;

    [ObservableProperty]
    private AppTab _activeTab = AppTab.Convert;

    public bool IsConvertTab => ActiveTab == AppTab.Convert;
    public bool IsBatchTab => ActiveTab == AppTab.Batch;
    public bool IsHistoryTab => ActiveTab == AppTab.History;
    public bool IsAboutTab => ActiveTab == AppTab.About;

    partial void OnActiveTabChanged(AppTab value)
    {
        OnPropertyChanged(nameof(IsConvertTab));
        OnPropertyChanged(nameof(IsBatchTab));
        OnPropertyChanged(nameof(IsHistoryTab));
        OnPropertyChanged(nameof(IsAboutTab));
    }

    [RelayCommand]
    private void GoToTab(AppTab tab) => ActiveTab = tab;

    partial void OnResizeModeChanged(ResizeMode value) => OnPropertyChanged(nameof(IsPercentageMode));

    partial void OnSourceAssetChanged(ImageAsset? value)
    {
        OnPropertyChanged(nameof(HasSource));
        ConvertCommand.NotifyCanExecuteChanged();
    }

    partial void OnResultAssetChanged(ImageAsset? value)
    {
        OnPropertyChanged(nameof(HasResult));
    }

    public void LoadDroppedFile(string path, ImageAsset asset)
    {
        SourcePath = path;
        SourceAsset = asset;
        ResultAsset = null;
        StatusMessage = null;
        LastRunFailed = false;

        SourcePreview?.Dispose();
        SourcePreview = TryLoadPreview(path);

        ResultPreview?.Dispose();
        ResultPreview = null;
    }

    private static readonly TimeSpan MinVisibleDuration = TimeSpan.FromMilliseconds(900);

    private CancellationTokenSource? _convertCts;

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (SourcePath is null || SourceAsset is null)
        {
            return;
        }

        IsConverting = true;
        StatusMessage = "Procesando...";
        LastRunFailed = false;
        ProgressFraction = 0;
        ProgressStepName = "Cargando";

        _convertCts = new CancellationTokenSource();
        var cancellationToken = _convertCts.Token;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var destinationDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Fragua");

            var operations = new List<IImageOperation>();

            // Mejorar calidad primero: sobre la resolucion mas original
            // posible. Ademas el upscaler no preserva canal alfa, asi que
            // si va despues de Quitar fondo se comeria la transparencia.
            if (UpscaleEnabled && IsUpscaleModelReady)
            {
                operations.Add(new UpscaleOperation(_upscaler));
            }

            if (RemoveBackgroundEnabled && IsModelReady)
            {
                operations.Add(new RemoveBackgroundOperation(_backgroundRemover));
            }

            if (ResizeEnabled)
            {
                var spec = BuildResizeSpec();
                if (spec is not null)
                {
                    operations.Add(new ResizeOperation(_resizer, spec));
                }
            }

            if (VectorizeEnabled)
            {
                operations.Add(new VectorizeOperation(_vectorizer, new VectorizeSpec(VectorizeColors), destinationDirectory));
            }
            else
            {
                operations.Add(new ConvertFormatOperation(_writer, destinationDirectory, TargetFormat, BuildOptimizeSpec()));
            }

            var job = new ImageJob(SourcePath, destinationDirectory, operations);

            // Magick.NET convierte una imagen en milisegundos: sin pacing la
            // barra saltaria de 0 a 100% en un parpadeo. El trabajo real ya
            // termino cuando se reporta cada paso; esto solo asegura que la
            // narracion visual del progreso alcance a verse, no inventa
            // tiempo de espera que no exista.
            var channel = Channel.CreateUnbounded<ImageJobProgress>();
            var progress = new Progress<ImageJobProgress>(p => channel.Writer.TryWrite(p));

            var pipelineTask = _pipeline.RunAsync(job, progress, cancellationToken);
            var drainTask = DrainProgressAsync(channel.Reader, cancellationToken);

            var result = await pipelineTask;
            channel.Writer.Complete();
            await drainTask;

            // Con una imagen chica y una sola operacion, el pipeline entero
            // puede terminar en menos tiempo del que dura un parpadeo. El
            // trabajo real ya esta hecho a esta altura; esto solo evita que
            // la barra desaparezca antes de que el ojo la registre.
            var remaining = MinVisibleDuration - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(remaining, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Se cancelo durante la espera de cierre; no cambia el
                    // resultado ya calculado.
                }
            }

            if (result.Succeeded && result.Output is not null)
            {
                ProgressFraction = 1;
                ResultAsset = result.Output;
                ResultPreview?.Dispose();
                ResultPreview = TryLoadPreview(result.Output.SourcePath);
                StatusMessage = $"Listo. Guardado en {destinationDirectory}";
                AddHistoryEntry(Path.GetFileName(SourcePath), SourceAsset.SizeBytes, result.Output);
            }
            else
            {
                LastRunFailed = true;
                StatusMessage = result.WasCancelled ? "Cancelado." : $"Error: {result.ErrorMessage}";
            }
        }
        finally
        {
            IsConverting = false;
            _convertCts?.Dispose();
            _convertCts = null;
        }
    }

    private bool CanCancelConvert() => IsConverting;

    [RelayCommand(CanExecute = nameof(CanCancelConvert))]
    private void CancelConvert() => _convertCts?.Cancel();

    private async Task DrainProgressAsync(ChannelReader<ImageJobProgress> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var p in reader.ReadAllAsync(cancellationToken))
            {
                ProgressFraction = p.StepFraction;
                ProgressStepName = p.CurrentStepName;
                await Task.Delay(220, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // El pipeline ya corto por su cuenta; el drain no tiene mas nada
            // que narrar, no es un error.
        }
    }

    private bool CanConvert() => HasSource && !IsConverting;

    private ResizeSpec? BuildResizeSpec()
    {
        if (ResizeMode == ResizeMode.Percentage)
        {
            return double.TryParse(WidthText, out var percentage) && percentage > 0
                ? new ResizeSpec(ResizeMode.Percentage, Percentage: percentage)
                : null;
        }

        var hasWidth = int.TryParse(WidthText, out var width) && width > 0;
        var hasHeight = int.TryParse(HeightText, out var height) && height > 0;

        return (hasWidth, hasHeight) switch
        {
            (true, true) => new ResizeSpec(ResizeMode, Width: width, Height: height),
            _ => null,
        };
    }

    partial void OnIsConvertingChanged(bool value)
    {
        ConvertCommand.NotifyCanExecuteChanged();
        CancelConvertCommand.NotifyCanExecuteChanged();
    }

    // --- Lotes: fase 1 en el plan, pero el pipeline de Core ya soporta
    // procesar varios archivos en paralelo (RunBatchAsync), asi que
    // exponerlo desde la interfaz no es trabajo nuevo, es cablear lo que
    // ya existe y esta probado. ---

    private static readonly string[] SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"];

    [ObservableProperty]
    private string? _batchFolder;

    [ObservableProperty]
    private bool _isBatchRunning;

    [ObservableProperty]
    private double _batchProgressFraction;

    [ObservableProperty]
    private int _batchCompletedCount;

    [ObservableProperty]
    private int _batchFailedCount;

    public ObservableCollection<BatchFileItem> BatchFiles { get; } = [];

    public bool HasBatchFolder => BatchFolder is not null;
    public bool HasBatchFiles => BatchFiles.Count > 0;
    public bool HasBatchResults => BatchCompletedCount > 0 || BatchFailedCount > 0;
    public bool HasBatchFailures => BatchFailedCount > 0;

    partial void OnBatchCompletedCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasBatchResults));
    }

    partial void OnBatchFailedCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasBatchResults));
        OnPropertyChanged(nameof(HasBatchFailures));
    }

    public void SetBatchFolder(string folder)
    {
        BatchFolder = folder;
        OnPropertyChanged(nameof(HasBatchFolder));

        BatchFiles.Clear();
        BatchCompletedCount = 0;
        BatchFailedCount = 0;
        BatchProgressFraction = 0;

        var files = Directory.EnumerateFiles(folder)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            BatchFiles.Add(new BatchFileItem { FileName = Path.GetFileName(file), FullPath = file });
        }

        OnPropertyChanged(nameof(HasBatchFiles));
        StartBatchCommand.NotifyCanExecuteChanged();
    }

    private bool CanStartBatch() => HasBatchFiles && !IsBatchRunning;
    private CancellationTokenSource? _batchCts;

    [RelayCommand(CanExecute = nameof(CanStartBatch))]
    private async Task StartBatchAsync()
    {
        if (BatchFolder is null || BatchFiles.Count == 0)
        {
            return;
        }

        IsBatchRunning = true;
        BatchCompletedCount = 0;
        BatchFailedCount = 0;
        BatchProgressFraction = 0;

        _batchCts = new CancellationTokenSource();
        var cancellationToken = _batchCts.Token;

        var destinationDirectory = Path.Combine(BatchFolder, "Fragua");
        var byPath = BatchFiles.ToDictionary(f => f.FullPath, f => f);

        foreach (var item in BatchFiles)
        {
            item.Status = BatchFileStatus.Pending;
            item.Detail = null;
        }

        try
        {
            var jobs = BatchFiles.Select(item =>
            {
                var operations = new List<IImageOperation>();
                if (UpscaleEnabled && IsUpscaleModelReady)
                {
                    operations.Add(new UpscaleOperation(_upscaler));
                }
                if (RemoveBackgroundEnabled && IsModelReady)
                {
                    operations.Add(new RemoveBackgroundOperation(_backgroundRemover));
                }
                if (ResizeEnabled)
                {
                    var spec = BuildResizeSpec();
                    if (spec is not null)
                    {
                        operations.Add(new ResizeOperation(_resizer, spec));
                    }
                }
                if (VectorizeEnabled)
                {
                    operations.Add(new VectorizeOperation(_vectorizer, new VectorizeSpec(VectorizeColors), destinationDirectory));
                }
                else
                {
                    operations.Add(new ConvertFormatOperation(_writer, destinationDirectory, TargetFormat, BuildOptimizeSpec()));
                }
                return new ImageJob(item.FullPath, destinationDirectory, operations);
            }).ToList();

            var progress = new Progress<ImageJobProgress>(p =>
            {
                if (byPath.TryGetValue(p.SourcePath, out var item) && item.Status != BatchFileStatus.Done && item.Status != BatchFileStatus.Failed)
                {
                    item.Status = BatchFileStatus.Running;
                    item.Detail = p.CurrentStepName;
                }
                BatchProgressFraction = jobs.Count == 0 ? 0 : (double)p.CompletedJobs / jobs.Count;
            });

            var maxParallelism = Math.Max(1, Environment.ProcessorCount / 2);

            try
            {
                var results = await _pipeline.RunBatchAsync(jobs, maxParallelism, progress, cancellationToken);

                foreach (var result in results)
                {
                    if (!byPath.TryGetValue(result.Job.SourcePath, out var item))
                    {
                        continue;
                    }

                    if (result.Succeeded && result.Output is not null)
                    {
                        item.Status = BatchFileStatus.Done;
                        item.Detail = "Listo";
                        BatchCompletedCount++;

                        var originalSize = new FileInfo(result.Job.SourcePath).Length;
                        AddHistoryEntry(item.FileName, originalSize, result.Output);
                    }
                    else
                    {
                        item.Status = BatchFileStatus.Failed;
                        item.Detail = result.WasCancelled ? "Cancelado" : result.ErrorMessage;
                        BatchFailedCount++;
                    }
                }

                BatchProgressFraction = 1;
            }
            catch (OperationCanceledException)
            {
                // A diferencia de un archivo que falla, cancelar el lote
                // entero corta Parallel.ForEachAsync con una excepcion (no
                // devuelve resultados parciales). Lo que ya estaba "Listo"
                // se queda asi; el resto pasa a Cancelado, no queda como si
                // nunca se hubiera tocado.
                foreach (var item in BatchFiles)
                {
                    if (item.Status is BatchFileStatus.Pending or BatchFileStatus.Running)
                    {
                        item.Status = BatchFileStatus.Failed;
                        item.Detail = "Cancelado";
                    }
                }
            }
        }
        finally
        {
            IsBatchRunning = false;
            _batchCts?.Dispose();
            _batchCts = null;
        }
    }

    private bool CanCancelBatch() => IsBatchRunning;

    [RelayCommand(CanExecute = nameof(CanCancelBatch))]
    private void CancelBatch() => _batchCts?.Cancel();

    partial void OnIsBatchRunningChanged(bool value)
    {
        StartBatchCommand.NotifyCanExecuteChanged();
        CancelBatchCommand.NotifyCanExecuteChanged();
    }

    // --- Historial de la sesion actual. Sin persistencia en disco todavia
    // (SQLite llega en fase 1, mismo criterio que ForgeMD); esto es el
    // registro real de "que se convirtio" mientras la app esta abierta, no
    // una pantalla vacia con una promesa. ---

    public ObservableCollection<HistoryEntry> History { get; } = [];

    public bool HasHistory => History.Count > 0;

    private void AddHistoryEntry(string fileName, long sizeBefore, ImageAsset output)
    {
        History.Insert(0, new HistoryEntry(fileName, sizeBefore, output.SizeBytes, output.Format.ToString(), DateTimeOffset.Now));
        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
        OnPropertyChanged(nameof(HasHistory));
    }

    /// <summary>
    /// Si la ventana se cierra a mitad de una conversion o un lote, el token
    /// de cancelacion no puede quedar huerfano.
    /// </summary>
    public void Dispose()
    {
        _convertCts?.Cancel();
        _convertCts?.Dispose();
        _batchCts?.Cancel();
        _batchCts?.Dispose();
        SourcePreview?.Dispose();
        ResultPreview?.Dispose();
    }
}
