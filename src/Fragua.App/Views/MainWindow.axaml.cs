using Avalonia.Controls;
using Avalonia.Input;
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
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        DropZone.Classes.Set("active", false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Set("active", false);

        var file = e.DataTransfer.TryGetFile();
        var path = file?.TryGetLocalPath();
        await LoadFileAsync(path);
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
}
