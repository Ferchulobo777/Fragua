using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IImageAssetLoader, MagickImageAssetLoader>();
        services.AddSingleton<IImageResizer, MagickImageResizer>();
        services.AddSingleton<IImageAssetWriter, MagickImageAssetWriter>();
        services.AddSingleton<ImagePipeline>();
        services.AddTransient<ConvertViewModel>();

        return services.BuildServiceProvider();
    }
}
