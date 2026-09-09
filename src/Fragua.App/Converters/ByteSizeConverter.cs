using System.Globalization;
using Avalonia.Data.Converters;

namespace Fragua.App.Converters;

/// <summary>
/// Formatea bytes como "8,2 MB" o "340 KB". El dato de peso antes/despues es
/// lo que el usuario vino a buscar (decision de UX del plan), asi que tiene
/// que leerse de un vistazo, no como un numero crudo de bytes.
/// </summary>
public sealed class ByteSizeConverter : IValueConverter
{
    public static readonly ByteSizeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return "-";
        }

        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes / (1024.0 * 1024.0):0.##} MB",
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
