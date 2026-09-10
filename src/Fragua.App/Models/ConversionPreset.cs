namespace Fragua.App.Models;

/// <summary>
/// Una combinacion de ajustes guardada con nombre ("Para marketplace",
/// "Adjunto de mail liviano"): un preset es un pipeline guardado, sale
/// gratis del diseno de arquitectura porque son los mismos campos que ya
/// expone ConvertViewModel.
/// </summary>
public sealed record ConversionPreset(
    string Name,
    bool ResizeEnabled,
    string ResizeMode,
    string WidthText,
    string HeightText,
    string TargetFormat,
    bool OptimizeEnabled,
    int OptimizeQuality,
    bool RemoveBackgroundEnabled,
    bool VectorizeEnabled,
    int VectorizeColors,
    bool UpscaleEnabled
);
