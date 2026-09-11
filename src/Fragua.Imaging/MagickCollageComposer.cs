using Fragua.Core.Operations;
using ImageMagick;

namespace Fragua.Imaging;

public sealed class MagickCollageComposer : ICollageComposer
{
    public Task<CollageResult> ComposeAsync(
        CollageSpec spec,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        if (spec.SourcePaths.Count == 0)
        {
            throw new ArgumentException("No hay imagenes para armar el collage.", nameof(spec));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(destinationDirectory);

        var background = spec.TransparentBackground ? MagickColors.Transparent : MagickColors.White;

        using var collection = new MagickImageCollection();
        foreach (var path in spec.SourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tile = new MagickImage(path);
            if (spec.CropToFill)
            {
                // Cubrir la celda (resize con FillArea, el "^" de ImageMagick)
                // y despues recortar el sobrante centrado: asi cada celda
                // sale exacta, sin distorsionar la imagen.
                tile.Resize(new MagickGeometry(spec.CellSize, spec.CellSize) { FillArea = true });
                tile.Extent(spec.CellSize, spec.CellSize, Gravity.Center, background);
            }

            collection.Add(tile);
        }

        var geometry = new MagickGeometry(spec.CellSize, spec.CellSize)
        {
            X = spec.SpacingPixels,
            Y = spec.SpacingPixels,
        };

        var settings = new MontageSettings
        {
            Geometry = geometry,
            TileGeometry = new MagickGeometry((uint)spec.Columns, 0),
            BackgroundColor = background,
            BorderWidth = 0,
            Shadow = false,
            Label = string.Empty,
            // Sin esto, ImageMagick ancla cada imagen mas chica que su celda
            // arriba a la izquierda en vez de centrarla: se nota feo en el
            // modo sin recorte, donde las celdas quedan de tamanos dispares.
            Gravity = Gravity.Center,
        };

        using var montage = collection.Montage(settings);
        var outputPath = NextAvailablePath(destinationDirectory, "collage", "png");
        montage.Write(outputPath);

        return Task.FromResult(new CollageResult(outputPath, montage.Width, montage.Height));
    }

    private static string NextAvailablePath(string directory, string baseName, string extension)
    {
        var candidate = Path.Combine(directory, $"{baseName}.{extension}");
        var counter = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName}-{counter}.{extension}");
            counter++;
        }

        return candidate;
    }
}
