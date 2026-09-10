namespace Fragua.App.Models;

/// <summary>
/// Una conversion ya terminada, para el registro de Historial de esta
/// sesion. Sin persistencia en disco todavia (eso es fase 1, con SQLite
/// como en ForgeMD); esto es el registro real de "que se convirtio" mientras
/// la app esta abierta.
/// </summary>
public sealed record HistoryEntry(
    string FileName,
    long SizeBeforeBytes,
    long SizeAfterBytes,
    string FormatAfter,
    DateTimeOffset When
);
