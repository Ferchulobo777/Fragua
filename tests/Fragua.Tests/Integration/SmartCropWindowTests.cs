using Fragua.Core.Operations;
using Fragua.Imaging;

namespace Fragua.Tests.Integration;

/// <summary>
/// La geometria de SmartCropWindow, sin el modelo: dado un centro de
/// sujeto ya conocido, confirma que la ventana de recorte tiene la
/// proporcion correcta, esta centrada en ese punto y nunca se sale de la
/// imagen original. La deteccion real del centro se prueba aparte, con
/// el modelo real, en SmartCropTests.
/// </summary>
public sealed class SmartCropWindowTests
{
    [Fact]
    public void Fuente_mas_ancha_que_el_destino_recorta_el_ancho_no_el_alto()
    {
        var window = MagickImageResizer.SmartCropWindow(
            originalWidth: 900, originalHeight: 500,
            targetWidth: 300, targetHeight: 300,
            center: new SubjectCenter(450, 250));

        Assert.Equal(500, (int)window.Height); // alto completo
        Assert.Equal(500, (int)window.Width);  // 1:1 sobre 500 de alto = 500 de ancho
    }

    [Fact]
    public void La_ventana_queda_centrada_en_el_sujeto_cuando_hay_lugar()
    {
        var window = MagickImageResizer.SmartCropWindow(
            originalWidth: 1000, originalHeight: 1000,
            targetWidth: 200, targetHeight: 200,
            center: new SubjectCenter(500, 500));

        // Ventana cuadrada de 1000x1000 (mismo aspecto que 200x200): el
        // sujeto centrado tiene que dar una ventana que cubra todo el lienzo.
        Assert.Equal(0, (int)window.X);
        Assert.Equal(0, (int)window.Y);
        Assert.Equal(1000, (int)window.Width);
        Assert.Equal(1000, (int)window.Height);
    }

    [Fact]
    public void Un_sujeto_cerca_del_borde_no_saca_la_ventana_de_la_imagen()
    {
        var window = MagickImageResizer.SmartCropWindow(
            originalWidth: 900, originalHeight: 500,
            targetWidth: 1, targetHeight: 1, // cuadrado
            center: new SubjectCenter(50, 250));

        Assert.True((int)window.X >= 0, $"X no puede ser negativo, fue {window.X}");
        Assert.True((int)window.X + (int)window.Width <= 900,
            $"La ventana se sale por la derecha: X={window.X}, Width={window.Width}");
    }

    [Fact]
    public void Un_sujeto_centrado_produce_el_mismo_recorte_que_el_centro_geometrico()
    {
        var window = MagickImageResizer.SmartCropWindow(
            originalWidth: 800, originalHeight: 400,
            targetWidth: 1, targetHeight: 1,
            center: new SubjectCenter(400, 200));

        // Aspecto 1:1 sobre un lienzo de 800x400: la ventana cuadrada mas
        // grande posible es 400x400, centrada en X.
        Assert.Equal(400, (int)window.Width);
        Assert.Equal(400, (int)window.Height);
        Assert.Equal(200, (int)window.X);
        Assert.Equal(0, (int)window.Y);
    }
}
