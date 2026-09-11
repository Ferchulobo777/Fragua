using Fragua.Core;
using Fragua.Core.Operations;

namespace Fragua.Tests.Fixtures;

/// <summary>
/// Para tests que no ejercitan ResizeMode.SmartCrop: MagickImageResizer
/// necesita la dependencia para construirse, pero mientras el modo sea
/// Exact/Fit/Percentage nunca se invoca.
/// </summary>
internal sealed class NullSubjectDetector : ISubjectDetector
{
    public Task<SubjectCenter> DetectCenterAsync(ImageAsset input, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "NullSubjectDetector no deberia invocarse: el test no uso ResizeMode.SmartCrop.");
}
