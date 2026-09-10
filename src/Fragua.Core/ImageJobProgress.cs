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
    int TotalJobs,
    int CompletedSteps = 0,
    int TotalSteps = 1
)
{
    /// <summary>
    /// Progreso 0..1 dentro del trabajo actual, calculado sobre los pasos de
    /// su propio pipeline (no sobre el lote). Una barra determinada usa esto,
    /// no una animacion indefinida que finge avanzar.
    /// </summary>
    public double StepFraction => TotalSteps <= 0 ? 0 : Math.Clamp((double)CompletedSteps / TotalSteps, 0, 1);
}
