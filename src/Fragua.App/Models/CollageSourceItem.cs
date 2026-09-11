namespace Fragua.App.Models;

/// <summary>Una imagen cargada para el collage, mostrada por nombre en la lista.</summary>
public sealed record CollageSourceItem(string FullPath)
{
    public string FileName => Path.GetFileName(FullPath);
}
