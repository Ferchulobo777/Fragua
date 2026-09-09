using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
            var result = await _pipeline.RunAsync(job, progress: null, CancellationToken.None);

            if (result.Succeeded)
            {
                ResultAsset = result.Output;
                StatusMessage = $"Listo. Guardado en {destinationDirectory}";
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
}
