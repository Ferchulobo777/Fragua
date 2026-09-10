using System.Globalization;
using Avalonia.Data.Converters;

namespace Fragua.App.Converters;

/// <summary>
/// Formatea una fraccion 0..1 como "45%", con la fuente monoespaciada de
/// cifras: es lo unico numerico que cambia mientras corre la conversion.
/// </summary>
public sealed class PercentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double fraction ? $"{(int)Math.Round(fraction * 100)}%" : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
