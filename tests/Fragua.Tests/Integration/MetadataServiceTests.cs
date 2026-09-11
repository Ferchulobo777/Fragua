using Fragua.Imaging;
using ImageMagick;

namespace Fragua.Tests.Integration;

/// <summary>
/// Prueba real con Magick.NET: escribe EXIF de verdad (incluyendo GPS,
/// para el caso mas sensible de privacidad) y confirma que la lectura y
/// el borrado selectivo funcionan sobre el archivo en disco, no solo en
/// memoria.
/// </summary>
public sealed class MetadataServiceTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), $"fragua-metadata-{Guid.NewGuid():N}");
    private readonly MagickMetadataService _service = new();

    public MetadataServiceTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    private string CreateJpegWithExif()
    {
        var path = Path.Combine(_workDir, $"fixture_{Guid.NewGuid():N}.jpg");

        using var image = new MagickImage(MagickColors.CornflowerBlue, 200, 200);
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.Make, "Fragua Test Camera");
        profile.SetValue(ExifTag.Model, "Model X");
        profile.SetValue(ExifTag.Software, "Fragua");
        profile.SetValue(ExifTag.GPSLatitude, new Rational[] { new(34), new(3), new(8) });
        profile.SetValue(ExifTag.GPSLatitudeRef, "N");
        image.SetProfile(profile);
        image.Write(path, MagickFormat.Jpg);

        return path;
    }

    [Fact]
    public async Task ReadAsync_devuelve_todos_los_campos_reales_del_archivo()
    {
        var path = CreateJpegWithExif();
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var fields = await _service.ReadAsync(asset, CancellationToken.None);

        Assert.Contains(fields, f => f.Tag == "Make" && f.Value == "Fragua Test Camera");
        Assert.Contains(fields, f => f.Tag == "Model" && f.Value == "Model X");
        Assert.Contains(fields, f => f.Tag == "GPSLatitude");
    }

    [Fact]
    public async Task RemoveFieldsAsync_saca_solo_los_campos_pedidos_los_demas_sobreviven()
    {
        var path = CreateJpegWithExif();
        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var destinationDirectory = Path.Combine(_workDir, "out");
        var result = await _service.RemoveFieldsAsync(
            asset,
            tagsToRemove: ["GPSLatitude", "GPSLatitudeRef"],
            destinationDirectory,
            CancellationToken.None);

        var resultAsset = await loader.LoadAsync(result.SourcePath, CancellationToken.None);
        var remainingFields = await _service.ReadAsync(resultAsset, CancellationToken.None);

        Assert.DoesNotContain(remainingFields, f => f.Tag == "GPSLatitude");
        Assert.DoesNotContain(remainingFields, f => f.Tag == "GPSLatitudeRef");
        Assert.Contains(remainingFields, f => f.Tag == "Make");
        Assert.Contains(remainingFields, f => f.Tag == "Model");
    }

    [Fact]
    public async Task ReadAsync_sobre_una_imagen_sin_exif_no_tira_excepcion_y_devuelve_vacio()
    {
        var path = Path.Combine(_workDir, $"fixture_{Guid.NewGuid():N}.png");
        using (var image = new MagickImage(MagickColors.CornflowerBlue, 100, 100))
        {
            image.Write(path, MagickFormat.Png);
        }

        var loader = new MagickImageAssetLoader();
        var asset = await loader.LoadAsync(path, CancellationToken.None);

        var fields = await _service.ReadAsync(asset, CancellationToken.None);

        Assert.Empty(fields);
    }
}
