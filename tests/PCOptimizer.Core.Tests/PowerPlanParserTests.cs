using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public class PowerPlanParserTests
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string Performance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    [Theory]
    [InlineData("Power Scheme GUID:", "Balanced")]
    [InlineData("GUID de plan de energía:", "Equilibrado")]
    [InlineData("GUID du mode de gestion de l’alimentation :", "Utilisation normale")]
    public void ReadsLocalizedLabelsAndActiveMarker(string prefix, string name)
    {
        var result = PowerPlanParser.Parse($"{prefix} {Balanced}  ({name}) *\r\n");
        var plan = Assert.Single(result);
        Assert.Equal(Guid.Parse(Balanced), plan.Id);
        Assert.Equal(name, plan.Name);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void PreservesActivePlanWhenItIsNotFirst()
    {
        var result = PowerPlanParser.Parse($"Existing Power Schemes\r\n---\r\nPower Scheme GUID: {Balanced} (Balanced)\r\nPower Scheme GUID: {Performance} (High performance) *\r\n");
        Assert.Equal(2, result.Count);
        Assert.False(result[0].IsActive);
        Assert.True(result[1].IsActive);
        Assert.Equal(Guid.Parse(Performance), result[1].Id);
    }

    [Fact]
    public void PreservesUserDefinedNamesWithParenthesesAndUnicode()
    {
        var plan = Assert.Single(PowerPlanParser.Parse($"{Balanced.ToUpperInvariant()} (Mi perfil (silencioso) — portátil) *"));
        Assert.Equal("Mi perfil (silencioso) — portátil", plan.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Access denied")]
    [InlineData("Power Scheme GUID: invalid (Balanced) *")]
    [InlineData("381b4222-f694-41f0-9685-ff5bb260df2e (Balanced) * unexpected")]
    public void DoesNotInventPlansForInvalidOutput(string output)
    {
        Assert.Empty(PowerPlanParser.Parse(output));
    }
}
