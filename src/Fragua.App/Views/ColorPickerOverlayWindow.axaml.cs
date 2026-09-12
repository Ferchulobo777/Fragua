using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Fragua.App.Views;

/// <summary>
/// Ventana transparente de pantalla completa (todos los monitores) para el
/// cuentagotas que toma color de cualquier parte de la pantalla, no solo de
/// la imagen cargada. El pixel se lee con GDI directo (Win32), no de la
/// composicion de Avalonia: asi funciona sobre cualquier ventana de
/// cualquier otra app, no solo dentro de Fragua.
/// </summary>
public partial class ColorPickerOverlayWindow : Window
{
    public string? PickedHex { get; private set; }

    public ColorPickerOverlayWindow()
    {
        InitializeComponent();

        PointerMoved += OnPointerMoved;
        PointerPressed += OnPointerPressed;
        KeyDown += OnKeyDown;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var virtualBounds = Screens.All
            .Select(s => s.Bounds)
            .Aggregate((a, b) => a.Union(b));

        Position = new PixelPoint(virtualBounds.X, virtualBounds.Y);
        Width = virtualBounds.Width;
        Height = virtualBounds.Height;

        Focus();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var localPosition = e.GetPosition(RootCanvas);
        var screenPosition = this.PointToScreen(e.GetPosition(this));
        var (r, g, b) = ScreenPixelReader.GetPixelColor(screenPosition.X, screenPosition.Y);
        var hex = $"#{r:X2}{g:X2}{b:X2}";

        HexLabel.Text = hex;
        SwatchPreview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));

        Canvas.SetLeft(InfoBadge, localPosition.X + 20);
        Canvas.SetTop(InfoBadge, localPosition.Y + 20);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            PickedHex = HexLabel.Text;
            Close();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            PickedHex = null;
            Close();
        }
    }
}
