using Fragua.Core;
using Fragua.Core.Operations;

namespace Fragua.Tests.Fixtures;

/// <summary>
/// Para tests que no ejercitan Quitar fondo: el ViewModel necesita la
/// dependencia para construirse, pero mientras RemoveBackgroundEnabled
/// quede en false (el default) nunca se invoca.
/// </summary>
internal sealed class NullBackgroundRemover : IBackgroundRemover
{
    public Task<ImageAsset> RemoveBackgroundAsync(ImageAsset input, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "NullBackgroundRemover no deberia invocarse: el test no activo RemoveBackgroundEnabled.");
}
