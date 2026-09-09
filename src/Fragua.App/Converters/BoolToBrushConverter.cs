using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Fragua.App.Converters;

/// <summary>
/// Elige entre dos brushes segun un bool: usado para el mensaje de estado,
/// que se lee en error (brasa-error) o neutro (texto tenue).
/// </summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public IBrush TrueBrush { get; set; } = Brushes.White;
    public IBrush FalseBrush { get; set; } = Brushes.Gray;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueBrush : FalseBrush;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
