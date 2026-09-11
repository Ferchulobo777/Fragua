using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fragua.App.Data;
using Fragua.App.ViewModels;
using Fragua.App.Views;
using Fragua.Core;
using Fragua.Core.Operations;
using Fragua.Imaging;
using Microsoft.Extensions.DependencyInjection;

namespace Fragua.App;

public partial class App : Application
{
    /// <summary>
    /// Contenedor de dependencias de toda la app. Simple a proposito: Fase 0
    /// tiene una sola pantalla, esto crece cuando aparezcan Lotes e Historial.
    /// </summary>
    public static ServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<ConvertViewModel>(),
            };
            desktop.Exit += (_, _) => Services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IImageAssetLoader, MagickImageAssetLoader>();
        services.AddSingleton<IImageAssetWriter, MagickImageAssetWriter>();
        services.AddSingleton<ImagePipeline>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<SiluetaModelProvider>();
        services.AddSingleton<IBackgroundRemover>(sp =>
            new OnnxBackgroundRemover(sp.GetRequiredService<SiluetaModelProvider>().ModelPath));
        // Mismo modelo que Quitar fondo (silueta.onnx): ResizeMode.SmartCrop
        // lo reusa para centrar el recorte sobre el sujeto detectado.
        services.AddSingleton<ISubjectDetector>(sp =>
            new OnnxSubjectDetector(sp.GetRequiredService<SiluetaModelProvider>().ModelPath));
        services.AddSingleton<IImageResizer, MagickImageResizer>();
        services.AddSingleton<IImageVectorizer, MagickImageVectorizer>();
        services.AddSingleton<UpscaleModelProvider>();
        services.AddSingleton<IImageUpscaler>(sp =>
            new OnnxImageUpscaler(sp.GetRequiredService<UpscaleModelProvider>().ModelPath));
        services.AddSingleton<FraguaDatabase>();
        services.AddSingleton<IIconSetGenerator, MagickIconSetGenerator>();
        services.AddTransient<ConvertViewModel>();

        return services.BuildServiceProvider();
    }
}
