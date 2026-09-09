namespace Fragua.Core;

/// <summary>
/// Un paso del pipeline. Cada implementacion real vive en Fragua.Imaging
/// (o en un motor externo, para el caso de vectorizar); Core solo conoce
/// este contrato, lo que permite testear el orden y la composicion del
/// pipeline con operaciones falsas, sin decodificar una imagen real.
/// </summary>
public interface IImageOperation
{
    /// <summary>
    /// Nombre corto para mostrar en la interfaz y en el log de un lote,
    /// por ejemplo "Redimensionar" o "Quitar fondo".
    /// </summary>
    string DisplayName { get; }

    Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken);
}
