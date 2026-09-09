using Fragua.Core;

namespace Fragua.Tests.Fakes;

internal sealed class FakeLoader : IImageAssetLoader
{
    private readonly ImageAsset _asset;

    public FakeLoader(ImageAsset? asset = null)
    {
        _asset = asset ?? new ImageAsset("origen.png", 800, 600, 120_000, ImageFormat.Png);
    }

    public Task<ImageAsset> LoadAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(_asset with { SourcePath = path });
}
