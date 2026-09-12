using System.Runtime.InteropServices;

namespace Fragua.App.Views;

/// <summary>
/// Lee un pixel de cualquier parte de la pantalla via GDI (Win32), no de
/// la composicion de Avalonia: por eso funciona sobre ventanas de
/// cualquier otra aplicacion, no solo dentro de Fragua. Solo Windows,
/// que es la unica plataforma que este instalador soporta.
/// </summary>
internal static class ScreenPixelReader
{
    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hwnd, nint hdc);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(nint hdc, int x, int y);

    public static (byte R, byte G, byte B) GetPixelColor(int x, int y)
    {
        var hdc = GetDC(0);
        try
        {
            var colorRef = GetPixel(hdc, x, y);
            var r = (byte)(colorRef & 0x000000FF);
            var g = (byte)((colorRef & 0x0000FF00) >> 8);
            var b = (byte)((colorRef & 0x00FF0000) >> 16);
            return (r, g, b);
        }
        finally
        {
            _ = ReleaseDC(0, hdc);
        }
    }
}
