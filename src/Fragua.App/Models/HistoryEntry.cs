namespace Fragua.App.Models;

/// <summary>
/// Una conversion ya terminada. Persistido en SQLite (ver
/// Data/FraguaDatabase.cs), sobrevive a cerrar la app.
/// </summary>
public sealed record HistoryEntry(
    string FileName,
    long SizeBeforeBytes,
    long SizeAfterBytes,
    string FormatAfter,
    DateTimeOffset When
);
