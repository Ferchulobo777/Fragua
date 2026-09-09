namespace Fragua.Core;

/// <summary>
/// Progreso de un trabajo dentro de un lote: que archivo, que paso del
/// pipeline esta corriendo ahora, y cuantos trabajos van completos sobre
/// el total. Con esto la barra de progreso muestra un dato real, no un
/// porcentaje que avanza solo.
/// </summary>
public sealed record ImageJobProgress(
    string SourcePath,
    string CurrentStepName,
    int CompletedJobs,
    int TotalJobs
);
