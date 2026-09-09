using Fragua.Core;

namespace Fragua.Tests.Fakes;

/// <summary>
/// Operacion falsa para testear el pipeline sin decodificar imagenes reales.
/// Registra si fue invocada y opcionalmente falla o se cuelga hasta que se
/// cancele el token, para probar el manejo de errores y de cancelacion.
/// </summary>
internal sealed class FakeOperation : IImageOperation
{
    private readonly Func<ImageAsset, ImageAsset>? _transform;
    private readonly Exception? _throws;
    private readonly bool _waitForCancellation;

    public FakeOperation(
        string displayName,
        Func<ImageAsset, ImageAsset>? transform = null,
        Exception? throws = null,
        bool waitForCancellation = false)
    {
        DisplayName = displayName;
        _transform = transform;
        _throws = throws;
        _waitForCancellation = waitForCancellation;
    }

    public string DisplayName { get; }
    public int CallCount { get; private set; }

    public async Task<ImageAsset> ApplyAsync(ImageAsset input, CancellationToken cancellationToken)
    {
        CallCount++;

        if (_waitForCancellation)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        if (_throws is not null)
        {
            throw _throws;
        }

        return _transform?.Invoke(input) ?? input;
    }
}
