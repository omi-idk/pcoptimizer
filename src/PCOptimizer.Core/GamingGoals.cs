namespace PCOptimizer.Core;

public enum GamingGoal { Balanced, Competitive, Streaming, Laptop }
public sealed record GamingGoalPlan(SavedTweakProfile Tweaks, string Explanation, string Validation);
public static class GamingGoals
{
    public static GamingGoalPlan Plan(GamingGoal goal, bool usesGameBarCapture)
    {
        if (!Enum.IsDefined(goal)) throw new InvalidDataException("Objetivo desconocido.");
        var ids = new List<string> { "game-mode" };
        if (goal == GamingGoal.Competitive && !usesGameBarCapture) ids.Add("game-capture");
        var explanation = goal switch
        {
            GamingGoal.Competitive => "Objetivo competitivo: Modo Juego y, únicamente si declaraste no usar clips Game Bar, desactivar su captura. No se quitan topes de FPS ni se fuerzan Reflex, HAGS, afinidad o threads.",
            GamingGoal.Streaming => "Gaming + streaming: conservar grabadores, overlays y comunicación. Solo se propone Modo Juego; configurar OBS, codificador y margen GPU requiere medir la escena real.",
            GamingGoal.Laptop => "Laptop: solo Modo Juego. Conservar energía, límites térmicos y utilidades del fabricante; no se fuerza alto rendimiento ni se desactiva administración de batería.",
            _ => "Equilibrado: solo Modo Juego. Las preferencias de apariencia y privacidad se eligen por separado; no se presentan como mejoras de FPS."
        };
        var label = goal switch { GamingGoal.Balanced => "Equilibrado", GamingGoal.Competitive => "Competitivo", GamingGoal.Streaming => "Gaming + streaming", _ => "Laptop gaming" };
        var profile = new SavedTweakProfile(1, label, ids);
        TweakProfiles.Validate(profile);
        return new(profile, explanation, "La selección no aplica cambios. Revisa valores y riesgos en Ajustes de Windows; usa su respaldo/punto de restauración y verifica el resultado. Valida con 3–5 repeticiones A/B; ningún objetivo demuestra beneficio por sí mismo.");
    }
}
public sealed record WorkloadExperiment(int SchemaVersion, ProcessWorkloadCapture Capture, WorkloadContext Context, GamingGoal Goal, bool UsesGameBarCapture)
{
    public static void Validate(WorkloadExperiment experiment)
    {
        if (experiment.SchemaVersion != 1 || experiment.Capture is null || experiment.Context is null || !Enum.IsDefined(experiment.Goal))
            throw new InvalidDataException("Experimento de procesos inválido.");
        ProcessWorkloads.Validate(experiment.Capture);
        foreach (var identity in new[] { experiment.Context.Game, experiment.Context.Collector })
            if (identity is not null && !experiment.Capture.Before.Concat(experiment.Capture.After).Any(r => r.Identity == identity))
                throw new InvalidDataException("El contexto refiere un proceso ajeno a la captura.");
        if (experiment.Context.Streaming != (experiment.Goal == GamingGoal.Streaming)) throw new InvalidDataException("Objetivo y contexto no coinciden.");
    }
}
