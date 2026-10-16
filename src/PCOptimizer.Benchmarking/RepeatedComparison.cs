namespace PCOptimizer.Benchmarking;

public sealed record RepeatedComparisonResult(int RepetitionsA, int RepetitionsB, double? MeanFpsA,
    double? MeanFpsB, double? DifferenceFps, double? Lower95Fps, double? Upper95Fps, string Conclusion);

public static class RepeatedComparison
{
    public static RepeatedComparisonResult Calculate(IReadOnlyList<BenchmarkSession> a, IReadOnlyList<BenchmarkSession> b)
    {
        if (a.Count is < 3 or > 30 || b.Count is < 3 or > 30)
            return new(a.Count, b.Count, null, null, null, null, null, "Inconcluso: se requieren 3–30 repeticiones independientes por configuración; empieza con 3–5 alternadas.");
        var sessions = a.Concat(b).ToArray();
        foreach (var session in sessions) BenchmarkSessionStore.Validate(session);
        if (sessions.Select(s => (s.SourceSha256, s.Stream)).Distinct().Count() != sessions.Length)
            throw new InvalidOperationException("Una captura/stream aparece más de una vez. Los frames de una captura no son repeticiones independientes.");
        foreach (var session in sessions.Skip(1))
            if (!BenchmarkStatistics.ContextMatches(sessions[0], session))
                return new(a.Count, b.Count, null, null, null, null, null, "Inconcluso: contextos incompletos o diferentes. Conserva hardware, escena, versión y ajustes de control; identifica la variable A/B en el escenario.");
        var x = a.Select(s => BenchmarkStatistics.Calculate(s.Frames).AverageFps).ToArray();
        var y = b.Select(s => BenchmarkStatistics.Calculate(s.Frames).AverageFps).ToArray();
        var mx = x.Average(); var my = y.Average(); var difference = my - mx;
        double Variance(double[] values, double mean) => values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1);
        var vx = Variance(x, mx) / x.Length; var vy = Variance(y, my) / y.Length;
        var se = Math.Sqrt(vx + vy);
        if (se == 0)
            return new(x.Length, y.Length, mx, my, difference, null, null, "Inconcluso para incertidumbre: no se observó variación entre repeticiones. No se inventa un intervalo de confianza cero.");
        var degrees = (vx + vy) * (vx + vy) / (vx * vx / (x.Length - 1) + vy * vy / (y.Length - 1));
        // Conservative tabulation: select a degrees-of-freedom value no larger than Welch's df.
        var criticals = new (double Df, double T)[] { (1,12.7063), (2,4.3027), (3,3.1825), (4,2.7765), (5,2.5706), (6,2.4469),
            (7,2.3647), (8,2.3061), (9,2.2622), (10,2.2282), (12,2.1789), (15,2.1315), (20,2.0860), (25,2.0596), (30,2.0423) };
        var critical = criticals.Last(t => t.Df <= Math.Max(1, degrees)).T;
        var lower = difference - critical * se; var upper = difference + critical * se;
        var conclusion = lower > 0 ? "Diferencia positiva observada de FPS presentados." : upper < 0
            ? "Regresión observada de FPS presentados." : "Inconcluso: el intervalo incluye cero.";
        return new(x.Length, y.Length, mx, my, difference, lower, upper, conclusion +
            " IC aproximado del 95% para B−A (Welch con t conservadora), suponiendo medias independientes y aproximadamente normales. No prueba causalidad; revisa lows, stuttering, orden y duración antes de recomendar un cambio.");
    }
}
