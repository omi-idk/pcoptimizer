using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public class CatalogSearchTests
{
    [Theory]
    [InlineData("comunicacion", "Comunicación")]
    [InlineData("DIAGNOSTICO", "Diagnóstico y redes")]
    [InlineData("firefox", "Mozilla.Firefox")]
    [InlineData("", "Steam")]
    [InlineData("   ", "Steam")]
    public void FindsCaseAndAccentIndependentMatches(string query, string field)
    {
        Assert.True(CatalogSearch.Matches(query, field));
    }

    [Fact]
    public void AllSearchTermsMustMatchButCanSpanFields()
    {
        Assert.True(CatalogSearch.Matches("firefox navegadores", "Firefox ESR", "Navegadores"));
        Assert.False(CatalogSearch.Matches("firefox discord", "Firefox ESR", "Navegadores"));
    }
}
