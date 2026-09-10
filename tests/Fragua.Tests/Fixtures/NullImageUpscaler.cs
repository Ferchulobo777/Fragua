using Fragua.Core;
using Fragua.Core.Operations;

namespace Fragua.Tests.Fixtures;

/// <summary>
/// Para tests que no ejercitan Mejorar calidad: el ViewModel necesita la
/// dependencia para construirse, pero mientras UpscaleEnabled quede en
/// false (el default) nunca se invoca.
/// </summary>
internal sealed class NullImageUpscaler : IImageUpscaler
{
    public Task<ImageAsset> UpscaleAsync(ImageAsset input, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "NullImageUpscaler no deberia invocarse: el test no activo UpscaleEnabled.");
}
