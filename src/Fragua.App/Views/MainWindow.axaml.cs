using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Fragua.App.ViewModels;
using Fragua.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Fragua.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFile = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFile ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.Classes.Set("active", hasFile);
        IconsDropZone.Classes.Set("active", hasFile);
        MetadataDropZone.Classes.Set("active", hasFile);
        PaletteDropZone.Classes.Set("active", hasFile);
        CollageDropZone.Classes.Set("active", hasFile);
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        DropZone.Classes.Set("active", false);
        IconsDropZone.Classes.Set("active", false);
        MetadataDropZone.Classes.Set("active", false);
        PaletteDropZone.Classes.Set("active", false);
        CollageDropZone.Classes.Set("active", false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Set("active", false);
        IconsDropZone.Classes.Set("active", false);
        MetadataDropZone.Classes.Set("active", false);
        PaletteDropZone.Classes.Set("active", false);
        CollageDropZone.Classes.Set("active", false);

        if (DataContext is ConvertViewModel { IsCollageTab: true } collageVm)
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files is not null)
            {
                var paths = files
                    .Select(f => f.TryGetLocalPath())
                    .Where(p => p is not null)
                    .Cast<string>();
                collageVm.AddCollageFiles(paths);
            }

            return;
        }

        var file = e.DataTransfer.TryGetFile();
        var path = file?.TryGetLocalPath();
        await LoadFileAsync(path);
    }

    private async void OnBrowseCollageFilesClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Elegir imagenes para el collage",
            AllowMultiple = true,
        });

        if (DataContext is not ConvertViewModel vm)
        {
            return;
        }

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>();
        vm.AddCollageFiles(paths);
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Elegir una imagen",
            AllowMultiple = false,
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        await LoadFileAsync(path);
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Elegir una carpeta",
            AllowMultiple = false,
        });

        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (path is null || DataContext is not ConvertViewModel vm)
        {
            return;
        }

        vm.SetBatchFolder(path);
    }

    private async Task LoadFileAsync(string? path)
    {
        if (path is null || DataContext is not ConvertViewModel vm)
        {
            return;
        }

        var loader = App.Services.GetRequiredService<IImageAssetLoader>();
        try
        {
            var asset = await loader.LoadAsync(path, CancellationToken.None);
            vm.LoadDroppedFile(path, asset);
        }
        catch (Exception)
        {
            // Archivo que no es una imagen valida: se ignora en silencio en
            // esta primera version, en vez de reventar la ventana.
        }
    }

    private async void OnCopyHexClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string hex } || DataContext is not ConvertViewModel vm)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(hex);
        vm.PaletteStatusMessage = $"Copiado {hex} al portapapeles.";
    }

    private async void OnPickColorFromImage(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image image || DataContext is not ConvertViewModel { SourceAsset: { } asset } vm)
        {
            return;
        }

        var controlWidth = image.Bounds.Width;
        var controlHeight = image.Bounds.Height;
        if (controlWidth <= 0 || controlHeight <= 0)
        {
            return;
        }

        // Image usa Stretch="Uniform": la imagen real puede quedar mas chica
        // que el control (barras vacias arriba/abajo o a los costados), asi
        // que hay que calcular su rectangulo real antes de mapear el click.
        var imageAspect = (double)asset.WidthPixels / asset.HeightPixels;
        var controlAspect = controlWidth / controlHeight;

        double displayedWidth, displayedHeight, offsetX, offsetY;
        if (imageAspect > controlAspect)
        {
            displayedWidth = controlWidth;
            displayedHeight = controlWidth / imageAspect;
            offsetX = 0;
            offsetY = (controlHeight - displayedHeight) / 2;
        }
        else
        {
            displayedHeight = controlHeight;
            displayedWidth = controlHeight * imageAspect;
            offsetY = 0;
            offsetX = (controlWidth - displayedWidth) / 2;
        }

        var position = e.GetPosition(image);
        var localX = position.X - offsetX;
        var localY = position.Y - offsetY;
        if (localX < 0 || localY < 0 || localX > displayedWidth || localY > displayedHeight)
        {
            return;
        }

        await vm.PickColorFromImageAsync(localX / displayedWidth, localY / displayedHeight);
    }
}
