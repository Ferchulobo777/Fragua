using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Fragua.App.Models;

namespace Fragua.App.Converters;

/// <summary>
/// Color del punto de estado en la tabla de lotes: gris pendiente, brasa
/// procesando, verde listo, rojo error. Un vistazo a la columna dice como va
/// el lote entero sin leer una palabra.
/// </summary>
public sealed class BatchStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            BatchFileStatus.Pending => Brushes.Gray,
            BatchFileStatus.Running => new SolidColorBrush(Color.Parse("#F27636")),
            BatchFileStatus.Done => new SolidColorBrush(Color.Parse("#6FCB9F")),
            BatchFileStatus.Failed => new SolidColorBrush(Color.Parse("#E0605A")),
            _ => Brushes.Gray,
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
