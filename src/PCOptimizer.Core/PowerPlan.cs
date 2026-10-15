using System.Text.RegularExpressions;

namespace PCOptimizer.Core;

public sealed record PowerPlan(Guid Id, string Name, bool IsActive)
{
    public string DisplayName => IsActive ? $"{Name} · Activo" : Name;
}

public static partial class PowerPlanParser
{
    // powercfg localizes its labels; GUID, parentheses and active marker are stable.
    [GeneratedRegex(@"(?<id>[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})\s+\((?<name>.*)\)\s*(?<active>\*)?\s*$")]
    private static partial Regex PlanPattern();

    public static IReadOnlyList<PowerPlan> Parse(string output)
    {
        var plans = new List<PowerPlan>();
        foreach (var line in output.Split('\n'))
        {
            var match = PlanPattern().Match(line.Trim());
            if (match.Success)
                plans.Add(new PowerPlan(Guid.Parse(match.Groups["id"].Value),
                    match.Groups["name"].Value, match.Groups["active"].Success));
        }
        return plans;
    }
}
