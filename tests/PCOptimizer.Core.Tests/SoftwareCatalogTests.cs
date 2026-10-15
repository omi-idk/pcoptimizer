using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public class SoftwareCatalogTests
{
    [Theory]
    [InlineData("Microsoft.WindowsStore")]
    [InlineData("Microsoft.GamingServices")]
    [InlineData("Microsoft.XboxIdentityProvider")]
    [InlineData("*")]
    [InlineData("Clipchamp.Clipchamp'; Remove-Item C:\\ -Recurse; '")]
    public void RejectsProtectedOrArbitraryRemovalTargets(string id)
    {
        Assert.Throws<ArgumentException>(() => SoftwareCatalog.GetOptionalApp(id));
    }

    [Theory]
    [InlineData("Valve.Steam --ignore-security-hash")]
    [InlineData("Unknown.Package")]
    [InlineData("*")]
    public void RejectsUnknownInstallTargets(string id)
    {
        Assert.Throws<ArgumentException>(() => SoftwareCatalog.InstallArguments(id));
    }

    [Fact]
    public void InstallKeepsAgreementAndIntegrityChecksAndDoesNotUpgradeExistingApps()
    {
        var args = SoftwareCatalog.InstallArguments("Valve.Steam");
        Assert.Equal("Valve.Steam", args[args.ToList().IndexOf("--id") + 1]);
        Assert.Contains("--exact", args);
        Assert.Contains("--interactive", args);
        Assert.Contains("--no-upgrade", args);
        Assert.DoesNotContain("--accept-package-agreements", args);
        Assert.DoesNotContain("--accept-source-agreements", args);
        Assert.DoesNotContain("--ignore-security-hash", args);
        Assert.DoesNotContain("--silent", args);
    }
}
