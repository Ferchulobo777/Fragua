using System.Collections.ObjectModel;
using System.Threading.Channels;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fragua.App.Data;
using Fragua.App.Models;
using Fragua.Core;
using Fragua.Core.Operations;
using Fragua.Imaging;

namespace Fragua.App.ViewModels;

/// <summary>
/// Convertir, Lotes, Historial y Acerca de: toda la interfaz corre sobre
/// este unico ViewModel, chico a proposito para lo que es hoy la app.
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
    private readonly FraguaDatabase _database;
    private readonly IIconSetGenerator _iconSetGenerator;
    private readonly IWatermarker _watermarker;
    private readonly IMetadataService _metadataService;
    private readonly IColorPaletteExtractor _paletteExtractor;
    private readonly ICollageComposer _collageComposer;
    private readonly IPixelColorReader _pixelColorReader;

    public ConvertViewModel(
        ImagePipeline pipeline,
        IImageResizer resizer,
        IImageAssetWriter writer,
        IBackgroundRemover backgroundRemover,
        SiluetaModelProvider modelProvider,
        IImageVectorizer vectorizer,
        IImageUpscaler upscaler,
        UpscaleModelProvider upscaleModelProvider,
        FraguaDatabase database,
        IIconSetGenerator iconSetGenerator,
        IWatermarker watermarker,
        IMetadataService metadataService,
        IColorPaletteExtractor paletteExtractor,
        ICollageComposer collageComposer,
        IPixelColorReader pixelColorReader)
    {
        _pipeline = pipeline;
        _resizer = resizer;
        _writer = writer;
        _backgroundRemover = backgroundRemover;
        _vectorizer = vectorizer;
        _modelProvider = modelProvider;
        _upscaler = upscaler;
        _upscaleModelProvider = upscaleModelProvider;
        _database = database;
        _iconSetGenerator = iconSetGenerator;
        _watermarker = watermarker;
        _metadataService = metadataService;
        _paletteExtractor = paletteExtractor;
        _collageComposer = collageComposer;
        _pixelColorReader = pixelColorReader;

        LoadHistoryFromDatabase();
        LoadPresetsFromDatabase();
        SyncFormatSelection();
        SyncResizeModeSelection();
        SyncWatermarkPositionSelection();
        SyncHarmonySelection();
        RefreshHarmony();
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
        if (!value)
        {
            return;
        }

        await EnsureSiluetaModelDownloadedAsync(onFailure: () => RemoveBackgroundEnabled = false);
    }

    /// <summary>
    /// silueta.onnx (~43MB) lo usan dos features (Quitar fondo y el modo
    /// Inteligente de Redimensionar): la primera que lo necesita lo baja,
    /// la segunda ya lo encuentra cacheado.
    /// </summary>
    private async Task EnsureSiluetaModelDownloadedAsync(Action onFailure)
    {
        if (IsModelReady || IsDownloadingModel)
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
            // Sin red o el servidor no respondio: se avisa y se revierte el
            // estado que disparo la descarga, no se deja la interfaz
            // esperando algo que no va a llegar.
            ModelDownloadError = "No se pudo descargar el modelo. Revisa la conexion e intenta de nuevo.";
            onFailure();
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

    // --- Marca de agua: texto superpuesto, sin IA, pura geometria y
    // opacidad con Magick.NET. Va despues de Redimensionar (el tamano de
    // letra escala con el resultado final) y no aplica si Vectorizar esta
    // activo (es un efecto raster, no tiene sentido sobre un SVG). ---

    [ObservableProperty]
    private bool _watermarkEnabled;

    [ObservableProperty]
    private string _watermarkText = "";

    [ObservableProperty]
    private double _watermarkOpacity = 0.5;

    public ObservableCollection<WatermarkPositionOption> WatermarkPositionOptions { get; } =
    [
        new(WatermarkPosition.TopLeft, "↖", "Arriba izquierda"),
        new(WatermarkPosition.TopRight, "↗", "Arriba derecha"),
        new(WatermarkPosition.Center, "•", "Centro"),
        new(WatermarkPosition.BottomLeft, "↙", "Abajo izquierda"),
        new(WatermarkPosition.BottomRight, "↘", "Abajo derecha"),
    ];

    [ObservableProperty]
    private WatermarkPosition _watermarkPosition = WatermarkPosition.BottomRight;

    [RelayCommand]
    private void SelectWatermarkPosition(WatermarkPositionOption option) => WatermarkPosition = option.Value;

    private void SyncWatermarkPositionSelection()
    {
        foreach (var option in WatermarkPositionOptions)
        {
            option.IsSelected = option.Value == WatermarkPosition;
        }
    }

    partial void OnWatermarkPositionChanged(WatermarkPosition value) => SyncWatermarkPositionSelection();

    private WatermarkOperation? BuildWatermarkOperation() =>
        WatermarkEnabled && !VectorizeEnabled && !string.IsNullOrWhiteSpace(WatermarkText)
            ? new WatermarkOperation(_watermarker, new WatermarkSpec(WatermarkText, WatermarkPosition, WatermarkOpacity))
            : null;

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

    // Svg queda afuera: es la salida de Vectorizar, no un formato al que se
    // pueda convertir desde el selector de "Formato de salida". El texto de
    // cada tarjeta es para el usuario final, no el nombre del enum.
    public ObservableCollection<FormatOption> FormatOptions { get; } =
    [
        new(ImageFormat.Png, "PNG", "PNG", "Con transparencia"),
        new(ImageFormat.Jpeg, "JPG", "JPEG", "Fotos, mas liviano"),
        new(ImageFormat.WebP, "WEBP", "WebP", "Web, buen balance"),
        new(ImageFormat.Avif, "AVIF", "AVIF", "Maxima compresion"),
        new(ImageFormat.Tiff, "TIFF", "TIFF", "Sin perdida"),
        new(ImageFormat.Bmp, "BMP", "BMP", "Sin comprimir"),
    ];

    public ObservableCollection<ResizeModeOption> ResizeModeOptions { get; } =
    [
        new(ResizeMode.Fit, "FIT", "Ajustar", "Mantiene la proporcion"),
        new(ResizeMode.Exact, "EX", "Exacto", "Puede distorsionar"),
        new(ResizeMode.Percentage, "%", "Porcentaje", "Del tamano original"),
        new(ResizeMode.SmartCrop, "IA", "Inteligente", "Centra el recorte en el sujeto"),
    ];

    // Logos reales (simple-icons), igual que los chips de Tecnologia en
    // Acerca de: nada de glifos inventados para las marcas. El de X va en
    // blanco en vez del negro oficial porque desaparece contra el tema
    // oscuro de la app; es un uso de marca que X permite sobre fondos oscuros.
    private const string IgIconData = "M7.0301.084c-1.2768.0602-2.1487.264-2.911.5634-.7888.3075-1.4575.72-2.1228 1.3877-.6652.6677-1.075 1.3368-1.3802 2.127-.2954.7638-.4956 1.6365-.552 2.914-.0564 1.2775-.0689 1.6882-.0626 4.947.0062 3.2586.0206 3.6671.0825 4.9473.061 1.2765.264 2.1482.5635 2.9107.308.7889.72 1.4573 1.388 2.1228.6679.6655 1.3365 1.0743 2.1285 1.38.7632.295 1.6361.4961 2.9134.552 1.2773.056 1.6884.069 4.9462.0627 3.2578-.0062 3.668-.0207 4.9478-.0814 1.28-.0607 2.147-.2652 2.9098-.5633.7889-.3086 1.4578-.72 2.1228-1.3881.665-.6682 1.0745-1.3378 1.3795-2.1284.2957-.7632.4966-1.636.552-2.9124.056-1.2809.0692-1.6898.063-4.948-.0063-3.2583-.021-3.6668-.0817-4.9465-.0607-1.2797-.264-2.1487-.5633-2.9117-.3084-.7889-.72-1.4568-1.3876-2.1228C21.2982 1.33 20.628.9208 19.8378.6165 19.074.321 18.2017.1197 16.9244.0645 15.6471.0093 15.236-.005 11.977.0014 8.718.0076 8.31.0215 7.0301.0839m.1402 21.6932c-1.17-.0509-1.8053-.2453-2.2287-.408-.5606-.216-.96-.4771-1.3819-.895-.422-.4178-.6811-.8186-.9-1.378-.1644-.4234-.3624-1.058-.4171-2.228-.0595-1.2645-.072-1.6442-.079-4.848-.007-3.2037.0053-3.583.0607-4.848.05-1.169.2456-1.805.408-2.2282.216-.5613.4762-.96.895-1.3816.4188-.4217.8184-.6814 1.3783-.9003.423-.1651 1.0575-.3614 2.227-.4171 1.2655-.06 1.6447-.072 4.848-.079 3.2033-.007 3.5835.005 4.8495.0608 1.169.0508 1.8053.2445 2.228.408.5608.216.96.4754 1.3816.895.4217.4194.6816.8176.9005 1.3787.1653.4217.3617 1.056.4169 2.2263.0602 1.2655.0739 1.645.0796 4.848.0058 3.203-.0055 3.5834-.061 4.848-.051 1.17-.245 1.8055-.408 2.2294-.216.5604-.4763.96-.8954 1.3814-.419.4215-.8181.6811-1.3783.9-.4224.1649-1.0577.3617-2.2262.4174-1.2656.0595-1.6448.072-4.8493.079-3.2045.007-3.5825-.006-4.848-.0608M16.953 5.5864A1.44 1.44 0 1 0 18.39 4.144a1.44 1.44 0 0 0-1.437 1.4424M5.8385 12.012c.0067 3.4032 2.7706 6.1557 6.173 6.1493 3.4026-.0065 6.157-2.7701 6.1506-6.1733-.0065-3.4032-2.771-6.1565-6.174-6.1498-3.403.0067-6.156 2.771-6.1496 6.1738M8 12.0077a4 4 0 1 1 4.008 3.9921A3.9996 3.9996 0 0 1 8 12.0077";
    private const string FbIconData = "M9.101 23.691v-7.98H6.627v-3.667h2.474v-1.58c0-4.085 1.848-5.978 5.858-5.978.401 0 .955.042 1.468.103a8.68 8.68 0 0 1 1.141.195v3.325a8.623 8.623 0 0 0-.653-.036 26.805 26.805 0 0 0-.733-.009c-.707 0-1.259.096-1.675.309a1.686 1.686 0 0 0-.679.622c-.258.42-.374.995-.374 1.752v1.297h3.919l-.386 2.103-.287 1.564h-3.246v8.245C19.396 23.238 24 18.179 24 12.044c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.628 3.874 10.35 9.101 11.647Z";
    private const string XIconData = "M14.234 10.162 22.977 0h-2.072l-7.591 8.824L7.251 0H.258l9.168 13.343L.258 24H2.33l8.016-9.318L16.749 24h6.993zm-2.837 3.299-.929-1.329L3.076 1.56h3.182l5.965 8.532.929 1.329 7.754 11.09h-3.182z";
    private const string InIconData = "M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433c-1.144 0-2.063-.926-2.063-2.065 0-1.138.92-2.063 2.063-2.063 1.14 0 2.064.925 2.064 2.063 0 1.139-.925 2.065-2.064 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z";
    private const string YtIconData = "M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z";
    private const string PtIconData = "M12.017 0C5.396 0 .029 5.367.029 11.987c0 5.079 3.158 9.417 7.618 11.162-.105-.949-.199-2.403.041-3.439.219-.937 1.406-5.957 1.406-5.957s-.359-.72-.359-1.781c0-1.663.967-2.911 2.168-2.911 1.024 0 1.518.769 1.518 1.688 0 1.029-.653 2.567-.992 3.992-.285 1.193.6 2.165 1.775 2.165 2.128 0 3.768-2.245 3.768-5.487 0-2.861-2.063-4.869-5.008-4.869-3.41 0-5.409 2.562-5.409 5.199 0 1.033.394 2.143.889 2.741.099.12.112.225.085.345-.09.375-.293 1.199-.334 1.363-.053.225-.172.271-.401.165-1.495-.69-2.433-2.878-2.433-4.646 0-3.776 2.748-7.252 7.92-7.252 4.158 0 7.392 2.967 7.392 6.923 0 4.135-2.607 7.462-6.233 7.462-1.214 0-2.354-.629-2.758-1.379l-.749 2.848c-.269 1.045-1.004 2.352-1.498 3.146 1.123.345 2.306.535 3.55.535 6.607 0 11.985-5.365 11.985-11.987C23.97 5.39 18.592.026 11.985.026L12.017 0z";

    public ObservableCollection<SocialPresetOption> SocialPresetOptions { get; } =
    [
        new("Instagram: post", 1080, 1080, IgIconData, "#FF0069"),
        new("Instagram: story", 1080, 1920, IgIconData, "#FF0069"),
        new("Instagram: vertical", 1080, 1350, IgIconData, "#FF0069"),
        new("Facebook: post", 1200, 630, FbIconData, "#0866FF"),
        new("Facebook: portada", 820, 312, FbIconData, "#0866FF"),
        new("X: post", 1600, 900, XIconData, "#FFFFFF"),
        new("X: portada", 1500, 500, XIconData, "#FFFFFF"),
        new("LinkedIn: post", 1200, 627, InIconData, "#0A66C2"),
        new("LinkedIn: portada", 1584, 396, InIconData, "#0A66C2"),
        new("YouTube: miniatura", 1280, 720, YtIconData, "#FF0000"),
        new("Pinterest: pin", 1000, 1500, PtIconData, "#BD081C"),
    ];

    [RelayCommand]
    private void SelectFormat(FormatOption option) => TargetFormat = option.Value;

    [RelayCommand]
    private void SelectResizeMode(ResizeModeOption option) => ResizeMode = option.Value;

    [RelayCommand]
    private void SelectSocialPreset(SocialPresetOption option)
    {
        ResizeEnabled = true;
        WidthText = option.Width.ToString();
        HeightText = option.Height.ToString();
        ResizeMode = ResizeMode.SmartCrop;
    }

    private void SyncFormatSelection()
    {
        foreach (var option in FormatOptions)
        {
            option.IsSelected = option.Value == TargetFormat;
        }
    }

    private void SyncResizeModeSelection()
    {
        foreach (var option in ResizeModeOptions)
        {
            option.IsSelected = option.Value == ResizeMode;
        }
    }

    partial void OnTargetFormatChanged(ImageFormat value) => SyncFormatSelection();

    public bool HasSource => SourceAsset is not null;
    public bool HasResult => ResultAsset is not null;
    public bool IsPercentageMode => ResizeMode == ResizeMode.Percentage;

    [ObservableProperty]
    private AppTab _activeTab = AppTab.Convert;

    public bool IsConvertTab => ActiveTab == AppTab.Convert;
    public bool IsBatchTab => ActiveTab == AppTab.Batch;
    public bool IsIconsTab => ActiveTab == AppTab.Icons;
    public bool IsMetadataTab => ActiveTab == AppTab.Metadata;
    public bool IsPaletteTab => ActiveTab == AppTab.Palette;
    public bool IsCollageTab => ActiveTab == AppTab.Collage;
    public bool IsHistoryTab => ActiveTab == AppTab.History;
    public bool IsAboutTab => ActiveTab == AppTab.About;

    async partial void OnActiveTabChanged(AppTab value)
    {
        OnPropertyChanged(nameof(IsConvertTab));
        OnPropertyChanged(nameof(IsBatchTab));
        OnPropertyChanged(nameof(IsIconsTab));
        OnPropertyChanged(nameof(IsMetadataTab));
        OnPropertyChanged(nameof(IsPaletteTab));
        OnPropertyChanged(nameof(IsCollageTab));
        OnPropertyChanged(nameof(IsHistoryTab));
        OnPropertyChanged(nameof(IsAboutTab));

        if (value == AppTab.Metadata)
        {
            await RefreshMetadataFieldsAsync();
        }
        else if (value == AppTab.Palette)
        {
            await RefreshPaletteAsync();
        }
    }

    [RelayCommand]
    private void GoToTab(AppTab tab) => ActiveTab = tab;

    async partial void OnResizeModeChanged(ResizeMode value)
    {
        OnPropertyChanged(nameof(IsPercentageMode));
        SyncResizeModeSelection();

        if (value == ResizeMode.SmartCrop)
        {
            await EnsureSiluetaModelDownloadedAsync(onFailure: () => ResizeMode = ResizeMode.Fit);
        }
    }

    partial void OnSourceAssetChanged(ImageAsset? value)
    {
        OnPropertyChanged(nameof(HasSource));
        ConvertCommand.NotifyCanExecuteChanged();
        GenerateIconsCommand.NotifyCanExecuteChanged();
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

        GeneratedIconFiles.Clear();
        IconGenerationError = null;
        OnPropertyChanged(nameof(HasGeneratedIcons));

        if (IsMetadataTab)
        {
            _ = RefreshMetadataFieldsAsync();
        }
        else
        {
            MetadataFields.Clear();
            OnPropertyChanged(nameof(HasMetadataFields));
        }

        if (IsPaletteTab)
        {
            _ = RefreshPaletteAsync();
        }
        else
        {
            PaletteColors.Clear();
            OnPropertyChanged(nameof(HasPaletteColors));
        }

        PickedColor = null;
        EyedropperError = null;
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

            var watermarkOperation = BuildWatermarkOperation();
            if (watermarkOperation is not null)
            {
                operations.Add(watermarkOperation);
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
                var watermarkOperation = BuildWatermarkOperation();
                if (watermarkOperation is not null)
                {
                    operations.Add(watermarkOperation);
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

    // --- Historial persistente (SQLite), mismo criterio que ForgeMD:
    // sobrevive a cerrar la app, no es un log de la sesion actual. ---

    public ObservableCollection<HistoryEntry> History { get; } = [];

    public bool HasHistory => History.Count > 0;

    private void LoadHistoryFromDatabase()
    {
        foreach (var entry in _database.LoadHistory())
        {
            History.Add(entry);
        }
        OnPropertyChanged(nameof(HasHistory));
    }

    private void AddHistoryEntry(string fileName, long sizeBefore, ImageAsset output)
    {
        var entry = new HistoryEntry(fileName, sizeBefore, output.SizeBytes, output.Format.ToString(), DateTimeOffset.Now);
        History.Insert(0, entry);
        _database.InsertHistoryEntry(entry);
        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
        _database.ClearHistory();
        OnPropertyChanged(nameof(HasHistory));
    }

    // --- Presets: una combinacion de ajustes guardada con nombre. Un
    // preset es un pipeline guardado, sale gratis del diseno porque son
    // los mismos campos que ya expone esta clase. ---

    public ObservableCollection<string> PresetNames { get; } = [];

    public bool HasPresets => PresetNames.Count > 0;

    [ObservableProperty]
    private string? _selectedPresetName;

    [ObservableProperty]
    private string _newPresetName = "";

    private void LoadPresetsFromDatabase()
    {
        PresetNames.Clear();
        foreach (var preset in _database.LoadPresets())
        {
            PresetNames.Add(preset.Name);
        }
        OnPropertyChanged(nameof(HasPresets));
    }

    private bool CanSavePreset() => !string.IsNullOrWhiteSpace(NewPresetName);

    [RelayCommand(CanExecute = nameof(CanSavePreset))]
    private void SavePreset()
    {
        var name = NewPresetName.Trim();
        var preset = new ConversionPreset(
            name,
            ResizeEnabled,
            ResizeMode.ToString(),
            WidthText,
            HeightText,
            TargetFormat.ToString(),
            OptimizeEnabled,
            OptimizeQuality,
            RemoveBackgroundEnabled,
            VectorizeEnabled,
            VectorizeColors,
            UpscaleEnabled);

        _database.SavePreset(preset);

        if (!PresetNames.Contains(name))
        {
            PresetNames.Add(name);
            OnPropertyChanged(nameof(HasPresets));
        }

        SelectedPresetName = name;
        NewPresetName = "";
    }

    partial void OnSelectedPresetNameChanged(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var preset = _database.LoadPresets().FirstOrDefault(p => p.Name == value);
        if (preset is null)
        {
            return;
        }

        ResizeEnabled = preset.ResizeEnabled;
        ResizeMode = Enum.Parse<ResizeMode>(preset.ResizeMode);
        WidthText = preset.WidthText;
        HeightText = preset.HeightText;
        TargetFormat = Enum.Parse<ImageFormat>(preset.TargetFormat);
        OptimizeEnabled = preset.OptimizeEnabled;
        OptimizeQuality = preset.OptimizeQuality;
        RemoveBackgroundEnabled = preset.RemoveBackgroundEnabled;
        VectorizeEnabled = preset.VectorizeEnabled;
        VectorizeColors = preset.VectorizeColors;
        UpscaleEnabled = preset.UpscaleEnabled;
    }

    [RelayCommand]
    private void DeleteSelectedPreset()
    {
        if (string.IsNullOrEmpty(SelectedPresetName))
        {
            return;
        }

        _database.DeletePreset(SelectedPresetName);
        PresetNames.Remove(SelectedPresetName);
        SelectedPresetName = null;
        OnPropertyChanged(nameof(HasPresets));
    }

    partial void OnNewPresetNameChanged(string value) => SavePresetCommand.NotifyCanExecuteChanged();

    // --- Iconos: de una imagen (logo, foto) genera todo el set de tamanos
    // que pide un icono de escritorio o un favicon, mas el .ico combinado.
    // Usa la misma imagen cargada en Convertir: no hace falta cargarla dos
    // veces, es "la imagen con la que estoy trabajando ahora". ---

    private static readonly int[] StandardIconSizes = [16, 32, 48, 64, 128, 256, 512];

    [ObservableProperty]
    private bool _includeIco = true;

    [ObservableProperty]
    private bool _isGeneratingIcons;

    [ObservableProperty]
    private string? _iconGenerationError;

    public ObservableCollection<string> GeneratedIconFiles { get; } = [];

    public bool HasGeneratedIcons => GeneratedIconFiles.Count > 0;

    private bool CanGenerateIcons() => HasSource && !IsGeneratingIcons;

    [RelayCommand(CanExecute = nameof(CanGenerateIcons))]
    private async Task GenerateIconsAsync()
    {
        if (SourcePath is null || SourceAsset is null)
        {
            return;
        }

        IsGeneratingIcons = true;
        IconGenerationError = null;
        GeneratedIconFiles.Clear();
        OnPropertyChanged(nameof(HasGeneratedIcons));

        try
        {
            var destinationDirectory = Path.Combine(
                Path.GetDirectoryName(SourcePath) ?? Directory.GetCurrentDirectory(), "Fragua");
            var spec = new IconSetSpec(StandardIconSizes, IncludeIco);
            var result = await _iconSetGenerator.GenerateAsync(SourceAsset, spec, destinationDirectory, CancellationToken.None);

            foreach (var file in result.GeneratedFiles)
            {
                GeneratedIconFiles.Add(Path.GetFileName(file));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            IconGenerationError = "No se pudo escribir en la carpeta de destino.";
        }
        finally
        {
            IsGeneratingIcons = false;
            OnPropertyChanged(nameof(HasGeneratedIcons));
        }
    }

    partial void OnIsGeneratingIconsChanged(bool value) => GenerateIconsCommand.NotifyCanExecuteChanged();

    // --- Metadatos: a diferencia de Optimizar (que saca todo con Strip),
    // esto deja ver que hay y elegir campo por campo. Usa la misma imagen
    // cargada en el resto de la app. ---

    public ObservableCollection<MetadataFieldItem> MetadataFields { get; } = [];

    public bool HasMetadataFields => MetadataFields.Count > 0;

    [ObservableProperty]
    private bool _isReadingMetadata;

    [ObservableProperty]
    private bool _isRemovingMetadata;

    partial void OnIsRemovingMetadataChanged(bool value) => RemoveSelectedMetadataCommand.NotifyCanExecuteChanged();

    [ObservableProperty]
    private string? _metadataError;

    [ObservableProperty]
    private string? _metadataStatusMessage;

    private async Task RefreshMetadataFieldsAsync()
    {
        MetadataFields.Clear();
        MetadataError = null;
        MetadataStatusMessage = null;
        OnPropertyChanged(nameof(HasMetadataFields));

        if (SourceAsset is null)
        {
            return;
        }

        IsReadingMetadata = true;
        try
        {
            var fields = await _metadataService.ReadAsync(SourceAsset, CancellationToken.None);
            foreach (var field in fields)
            {
                MetadataFields.Add(new MetadataFieldItem(field.Tag, field.Value));
            }

            OnPropertyChanged(nameof(HasMetadataFields));
            RemoveSelectedMetadataCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MetadataError = "No se pudo leer los metadatos de esta imagen.";
        }
        finally
        {
            IsReadingMetadata = false;
        }
    }

    // No depende de si algun campo especifico esta tildado: eso es una
    // propiedad de un item de la coleccion, no del ViewModel, y el toolkit
    // no vuelve a evaluar CanExecute por cambios en objetos anidados. En
    // vez de cablear esa notificacion, el comando simplemente no hace nada
    // si nadie tildo un campo.
    private bool CanRemoveSelectedMetadata() => !IsRemovingMetadata && HasMetadataFields;

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedMetadata))]
    private async Task RemoveSelectedMetadataAsync()
    {
        if (SourcePath is null || SourceAsset is null)
        {
            return;
        }

        var tagsToRemove = MetadataFields.Where(f => f.IsSelected).Select(f => f.Tag).ToList();
        if (tagsToRemove.Count == 0)
        {
            MetadataStatusMessage = "Marca al menos un campo para sacar.";
            return;
        }

        IsRemovingMetadata = true;
        MetadataError = null;
        MetadataStatusMessage = null;

        try
        {
            var destinationDirectory = Path.Combine(
                Path.GetDirectoryName(SourcePath) ?? Directory.GetCurrentDirectory(), "Fragua");
            var result = await _metadataService.RemoveFieldsAsync(
                SourceAsset, tagsToRemove, destinationDirectory, CancellationToken.None);

            MetadataStatusMessage = $"Guardado sin {tagsToRemove.Count} campo(s) en {Path.GetFileName(result.SourcePath)}.";
            await RefreshMetadataFieldsAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MetadataError = "No se pudo escribir en la carpeta de destino.";
        }
        finally
        {
            IsRemovingMetadata = false;
        }
    }

    [RelayCommand]
    private void SelectAllMetadata()
    {
        foreach (var field in MetadataFields)
        {
            field.IsSelected = true;
        }

        RemoveSelectedMetadataCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void DeselectAllMetadata()
    {
        foreach (var field in MetadataFields)
        {
            field.IsSelected = false;
        }

        RemoveSelectedMetadataCommand.NotifyCanExecuteChanged();
    }

    // --- Paleta: colores dominantes de la imagen cargada, ignorando fondo
    // transparente (el caso mas comun en logos). Sin IA, pura cuantizacion
    // con Magick.NET. ---

    public ObservableCollection<PaletteColorItem> PaletteColors { get; } = [];

    public bool HasPaletteColors => PaletteColors.Count > 0;

    [ObservableProperty]
    private bool _isExtractingPalette;

    [ObservableProperty]
    private string? _paletteError;

    [ObservableProperty]
    private string? _paletteStatusMessage;

    [ObservableProperty]
    private int _paletteColorCount = 6;

    // --- Cuentagotas: el color exacto de un punto que el usuario clickea
    // sobre la imagen cargada, no un promedio ni una cuantizacion. ---

    [ObservableProperty]
    private ColorSwatchItem? _pickedColor;

    [ObservableProperty]
    private string? _eyedropperError;

    public async Task PickColorFromImageAsync(double fractionX, double fractionY)
    {
        if (SourceAsset is null)
        {
            return;
        }

        try
        {
            var result = await _pixelColorReader.ReadAsync(SourceAsset, fractionX, fractionY, CancellationToken.None);
            PickedColor = new ColorSwatchItem(result.Hex);
            EyedropperError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            EyedropperError = "No se pudo leer el color de ese punto.";
        }
    }

    /// <summary>Lo llama el cuentagotas de pantalla completa (MainWindow), que lee el pixel via Win32.</summary>
    public void SetPickedColor(string hex)
    {
        PickedColor = new ColorSwatchItem(hex);
        EyedropperError = null;
    }

    // --- Generador de paletas: matematica de color pura (rotacion de
    // matiz), no necesita ninguna imagen cargada. Independiente del
    // cuentagotas y de la paleta automatica de arriba. ---

    public ObservableCollection<HarmonyOption> HarmonyOptions { get; } =
    [
        new(ColorHarmony.Complementary, "180", "Complementaria", "El color opuesto"),
        new(ColorHarmony.Analogous, "±30", "Analoga", "Vecinos del color"),
        new(ColorHarmony.Triadic, "120", "Triadica", "Tres colores equidistantes"),
        new(ColorHarmony.Monochromatic, "1H", "Monocromatica", "Mismo matiz, distinta luz"),
    ];

    public ObservableCollection<ColorSwatchItem> HarmonyColors { get; } = [];

    public bool HasHarmonyColors => HarmonyColors.Count > 0;

    [ObservableProperty]
    private string _harmonyBaseHex = "#F27636";

    [ObservableProperty]
    private ColorHarmony _harmonyType = ColorHarmony.Complementary;

    [ObservableProperty]
    private string? _harmonyError;

    [RelayCommand]
    private void SelectHarmony(HarmonyOption option) => HarmonyType = option.Value;

    private void SyncHarmonySelection()
    {
        foreach (var option in HarmonyOptions)
        {
            option.IsSelected = option.Value == HarmonyType;
        }
    }

    partial void OnHarmonyTypeChanged(ColorHarmony value)
    {
        SyncHarmonySelection();
        RefreshHarmony();
    }

    partial void OnHarmonyBaseHexChanged(string value) => RefreshHarmony();

    private void RefreshHarmony()
    {
        HarmonyColors.Clear();
        HarmonyError = null;

        try
        {
            foreach (var hex in ColorHarmonyGenerator.Generate(HarmonyBaseHex, HarmonyType))
            {
                HarmonyColors.Add(new ColorSwatchItem(hex));
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            HarmonyError = "Ese no es un color hexadecimal valido (ej: #F27636).";
        }

        OnPropertyChanged(nameof(HasHarmonyColors));
    }

    /// <summary>Copia el color elegido en el cuentagotas como base del generador.</summary>
    [RelayCommand]
    private void UsePickedColorAsHarmonyBase()
    {
        if (PickedColor is not null)
        {
            HarmonyBaseHex = PickedColor.Hex;
        }
    }

    private async Task RefreshPaletteAsync()
    {
        PaletteColors.Clear();
        PaletteError = null;
        PaletteStatusMessage = null;
        OnPropertyChanged(nameof(HasPaletteColors));

        if (SourceAsset is null)
        {
            return;
        }

        IsExtractingPalette = true;
        try
        {
            var colors = await _paletteExtractor.ExtractAsync(SourceAsset, PaletteColorCount, CancellationToken.None);
            foreach (var color in colors)
            {
                PaletteColors.Add(new PaletteColorItem(color.Hex, color.Percentage));
            }

            OnPropertyChanged(nameof(HasPaletteColors));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PaletteError = "No se pudo leer la imagen para sacar la paleta.";
        }
        finally
        {
            IsExtractingPalette = false;
        }
    }

    async partial void OnPaletteColorCountChanged(int value)
    {
        if (IsPaletteTab)
        {
            await RefreshPaletteAsync();
        }
    }

    // --- Collage: junta varias imagenes sueltas en una sola grilla. Con
    // espaciado en 0 y recorte activado da un sprite sheet bien pegado; con
    // espaciado y sin recorte, un collage tipo contacto que muestra cada
    // foto completa. ---

    public ObservableCollection<CollageSourceItem> CollageSources { get; } = [];

    public bool HasCollageSources => CollageSources.Count > 0;

    [ObservableProperty]
    private int _collageColumns = 3;

    [ObservableProperty]
    private int _collageCellSize = 320;

    [ObservableProperty]
    private int _collageSpacing = 12;

    [ObservableProperty]
    private bool _collageCropToFill = true;

    [ObservableProperty]
    private bool _collageTransparentBackground = true;

    [ObservableProperty]
    private bool _isComposingCollage;

    [ObservableProperty]
    private string? _collageError;

    [ObservableProperty]
    private string? _collageStatusMessage;

    public void AddCollageFiles(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(CollageSources.Select(s => s.FullPath));
        foreach (var path in paths)
        {
            if (existing.Add(path))
            {
                CollageSources.Add(new CollageSourceItem(path));
            }
        }

        OnPropertyChanged(nameof(HasCollageSources));
        ComposeCollageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveCollageSource(CollageSourceItem item)
    {
        CollageSources.Remove(item);
        OnPropertyChanged(nameof(HasCollageSources));
        ComposeCollageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearCollageSources()
    {
        CollageSources.Clear();
        CollageStatusMessage = null;
        OnPropertyChanged(nameof(HasCollageSources));
        ComposeCollageCommand.NotifyCanExecuteChanged();
    }

    private bool CanComposeCollage() => CollageSources.Count >= 2 && !IsComposingCollage;

    [RelayCommand(CanExecute = nameof(CanComposeCollage))]
    private async Task ComposeCollageAsync()
    {
        IsComposingCollage = true;
        CollageError = null;
        CollageStatusMessage = null;

        try
        {
            var destinationDirectory = Path.Combine(
                Path.GetDirectoryName(CollageSources[0].FullPath) ?? Directory.GetCurrentDirectory(), "Fragua");
            var spec = new CollageSpec(
                CollageSources.Select(s => s.FullPath).ToList(),
                CollageColumns,
                (uint)CollageCellSize,
                CollageSpacing,
                CollageCropToFill,
                CollageTransparentBackground);

            var result = await _collageComposer.ComposeAsync(spec, destinationDirectory, CancellationToken.None);
            CollageStatusMessage = $"Listo: {Path.GetFileName(result.OutputPath)} ({result.Width}x{result.Height}px) en {destinationDirectory}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            CollageError = "No se pudo armar el collage.";
        }
        finally
        {
            IsComposingCollage = false;
        }
    }

    partial void OnIsComposingCollageChanged(bool value) => ComposeCollageCommand.NotifyCanExecuteChanged();

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
