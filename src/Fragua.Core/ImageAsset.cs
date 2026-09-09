namespace Fragua.Core;

/// <summary>
/// Una imagen en un punto del pipeline. No envuelve un objeto de Magick.NET
/// ni ningun otro tipo de terceros: Core no conoce esas librerias, y una
/// operacion falsa en un test unitario tiene que poder construir uno de estos
/// sin tocar disco ni decodificar nada real.
/// </summary>
public sealed record ImageAsset(
    string SourcePath,
    int WidthPixels,
    int HeightPixels,
    long SizeBytes,
    ImageFormat Format
);
