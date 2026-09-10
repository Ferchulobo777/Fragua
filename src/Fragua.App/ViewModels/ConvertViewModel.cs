using System.Collections.ObjectModel;
using System.Threading.Channels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fragua.App.Models;
using Fragua.Core;
using Fragua.Core.Operations;

namespace Fragua.App.ViewModels;

/// <summary>
/// Fase 0 del plan: convertir y redimensionar, en una sola pasada. Nada de
/// lotes, presets ni historial todavia (eso es fase 1).
/// </summary>
public sealed partial class ConvertViewModel : ViewModelBase
{
    private readonly ImagePipeline _pipeline;
    private readonly IImageResizer _resizer;
    private readonly IImageAssetWriter _writer;

    public ConvertViewModel(ImagePipeline pipeline, IImageResizer resizer, IImageAssetWriter writer)
    {
        _pipeline = pipeline;
        _resizer = resizer;
        _writer = writer;
    }

    [ObservableProperty]
    private string? _sourcePath;

    [ObservableProperty]
    private ImageAsset? _sourceAsset;

    [ObservableProperty]
    private ImageAsset? _resultAsset;

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
    }

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

        try
        {
            var destinationDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Fragua");

            var operations = new List<IImageOperation>();

            if (ResizeEnabled)
            {
                var spec = BuildResizeSpec();
                if (spec is not null)
                {
                    operations.Add(new ResizeOperation(_resizer, spec));
                }
            }

            operations.Add(new ConvertFormatOperation(_writer, destinationDirectory, TargetFormat));

            var job = new ImageJob(SourcePath, destinationDirectory, operations);

            // Magick.NET convierte una imagen en milisegundos: sin pacing la
            // barra saltaria de 0 a 100% en un parpadeo. El trabajo real ya
            // termino cuando se reporta cada paso; esto solo asegura que la
            // narracion visual del progreso alcance a verse, no inventa
            // tiempo de espera que no exista.
            var channel = Channel.CreateUnbounded<ImageJobProgress>();
            var progress = new Progress<ImageJobProgress>(p => channel.Writer.TryWrite(p));

            var pipelineTask = _pipeline.RunAsync(job, progress, CancellationToken.None);
            var drainTask = DrainProgressAsync(channel.Reader);

            var result = await pipelineTask;
            channel.Writer.Complete();
            await drainTask;

            if (result.Succeeded && result.Output is not null)
            {
                ProgressFraction = 1;
                ResultAsset = result.Output;
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
        }
    }

    private async Task DrainProgressAsync(ChannelReader<ImageJobProgress> reader)
    {
        await foreach (var p in reader.ReadAllAsync())
        {
            ProgressFraction = p.StepFraction;
            ProgressStepName = p.CurrentStepName;
            await Task.Delay(220);
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

    partial void OnIsConvertingChanged(bool value) => ConvertCommand.NotifyCanExecuteChanged();

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
                if (ResizeEnabled)
                {
                    var spec = BuildResizeSpec();
                    if (spec is not null)
                    {
                        operations.Add(new ResizeOperation(_resizer, spec));
                    }
                }
                operations.Add(new ConvertFormatOperation(_writer, destinationDirectory, TargetFormat));
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
            var results = await _pipeline.RunBatchAsync(jobs, maxParallelism, progress, CancellationToken.None);

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
        finally
        {
            IsBatchRunning = false;
        }
    }

    partial void OnIsBatchRunningChanged(bool value) => StartBatchCommand.NotifyCanExecuteChanged();

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
}
