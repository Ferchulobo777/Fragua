namespace Fragua.Core.Operations;

/// <summary>
/// Mejora de calidad con IA local (super-resolucion 4x, Real-ESRGAN). Como
/// Redimensionar: transforma en memoria, no es terminal.
/// </summary>
public interface IImageUpscaler
{
    Task<ImageAsset> UpscaleAsync(ImageAsset input, CancellationToken cancellationToken);
}

public sealed class UpscaleOperation : IImageOperation
{
    private readonly IImageUpscaler _upscaler;

    public UpscaleOperation(IImageUpscaler upscaler)
    {
        _upscaler = upscaler;
    }

    public string DisplayName => "Mejorar calidad";

    public Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken) =>
        _upscaler.UpscaleAsync(input, cancellationToken);
}
