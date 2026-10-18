namespace PCOptimizer.Core;

public sealed record NetworkCounterRate(double? ReceiveBytesPerSecond, double? SendBytesPerSecond, string Detail)
{
    public static NetworkCounterRate Calculate(long receivedBefore, long receivedAfter, long sentBefore, long sentAfter, double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < .001 || receivedBefore < 0 || sentBefore < 0 || receivedAfter < receivedBefore || sentAfter < sentBefore)
            return new(null, null, "Contador reiniciado o intervalo inválido; no se calculó una tasa.");
        return new((receivedAfter - receivedBefore) / seconds, (sentAfter - sentBefore) / seconds,
            "Promedio de tráfico observado durante la ventana; no es un test de velocidad contratada ni de bufferbloat.");
    }
}
