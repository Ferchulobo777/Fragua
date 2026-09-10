namespace Fragua.Core.Operations;

/// <summary>
/// Quita el fondo con un modelo de segmentacion local (ONNX). Sin nube: la
/// unica IA de Fragua es esta, y corre entera en la maquina del usuario.
/// La implementacion real vive en Fragua.Imaging.
/// </summary>
public interface IBackgroundRemover
{
    Task<ImageAsset> RemoveBackgroundAsync(ImageAsset input, CancellationToken cancellationToken);
}

public sealed class RemoveBackgroundOperation : IImageOperation
{
    private readonly IBackgroundRemover _remover;

    public RemoveBackgroundOperation(IBackgroundRemover remover)
    {
        _remover = remover;
    }

    public string DisplayName => "Quitar fondo";

    public Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken) =>
        _remover.RemoveBackgroundAsync(input, cancellationToken);
}
