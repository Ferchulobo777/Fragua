namespace Fragua.Core.Operations;

/// <summary>
/// El centro del sujeto detectado, en pixeles de la imagen original. No es
/// un bounding box completo: para centrar un recorte alcanza con el punto,
/// y un punto es mas facil de calcular de forma robusta contra ruido de
/// fondo que un rectangulo exacto.
/// </summary>
public sealed record SubjectCenter(int X, int Y);

/// <summary>
/// Detecta donde esta el sujeto de una imagen con el mismo modelo de
/// segmentacion local que usa Quitar fondo (silueta.onnx). La implementacion
/// real vive en Fragua.Imaging.
/// </summary>
public interface ISubjectDetector
{
    Task<SubjectCenter> DetectCenterAsync(ImageAsset input, CancellationToken cancellationToken);
}
