using System.Globalization;

namespace PCOptimizer.Core;

public static class CatalogSearch
{
    public static bool Matches(string? query, params string[] fields)
    {
        var text = string.Join(" ", fields);
        return (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .All(term => CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, term,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
    }
}
