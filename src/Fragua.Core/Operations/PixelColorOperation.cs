namespace Fragua.Core.Operations;

public sealed record PixelColorResult(string Hex, byte R, byte G, byte B, byte Alpha);

/// <summary>
/// Cuentagotas: el color exacto de un punto de la imagen, dado como
/// fraccion 0..1 del ancho/alto (no pixeles), para no acoplar al llamador
/// con la resolucion real del archivo.
/// </summary>
public interface IPixelColorReader
{
    Task<PixelColorResult> ReadAsync(
        ImageAsset input,
        double fractionX,
        double fractionY,
        CancellationToken cancellationToken);
}
