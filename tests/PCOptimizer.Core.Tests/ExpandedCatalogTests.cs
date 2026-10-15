using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public class ExpandedCatalogTests
{
    [Fact]
    public void EmbeddedCatalogCoversTheScreenshotCategoriesAndHasUniqueTargets()
    {
        Assert.Equal(236, SoftwareCatalog.Packages.Count);
        Assert.Equal(10, SoftwareCatalog.Packages.Select(p => p.Category).Distinct().Count());
        Assert.Equal(236, SoftwareCatalog.Packages.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(SoftwareCatalog.Packages, p => p.Name == "ChatGPT Desktop");
        Assert.Contains(SoftwareCatalog.Packages, p => p.Name == "HWiNFO");
        Assert.Contains(SoftwareCatalog.Packages, p => p.Name == "Snappy Driver Installer Origin");
        Assert.Contains(SoftwareCatalog.Packages, p => p.Name == "Visual Studio 2026");
    }

    [Theory]
    [InlineData("msstore:9NT1R1C2HH7J", "9NT1R1C2HH7J")]
    [InlineData("msstore:9NKSQGP7F2NH", "9NKSQGP7F2NH")]
    public void StoreIdsUseStoreSourceWithoutPassingThePrefixAsPackageId(string id, string target)
    {
        var args = SoftwareCatalog.InstallArguments(id).ToArray();
        Assert.Equal(target, args[Array.IndexOf(args, "--id") + 1]);
        Assert.Equal("msstore", args[Array.IndexOf(args, "--source") + 1]);
        Assert.DoesNotContain("--interactive", args);
        Assert.DoesNotContain("--accept-source-agreements", args);
        Assert.DoesNotContain("--accept-package-agreements", args);
    }

    [Fact]
    public void EveryInstallTargetHasExplicitSourceAndPreservesVerificationAndNoUpgrade()
    {
        foreach (var package in SoftwareCatalog.Packages)
        {
            Assert.Equal("https", new Uri(package.Website).Scheme);
            Assert.NotEmpty(package.Description);
            var args = SoftwareCatalog.InstallArguments(package.Id).ToArray();
            Assert.Equal(package.PackageId, args[Array.IndexOf(args, "--id") + 1]);
            Assert.Equal(package.Source, args[Array.IndexOf(args, "--source") + 1]);
            Assert.Contains("--exact", args);
            Assert.Contains("--no-upgrade", args);
            Assert.DoesNotContain("--ignore-security-hash", args);
            Assert.DoesNotContain("--accept-package-agreements", args);
            Assert.DoesNotContain("--accept-source-agreements", args);
        }
    }

    [Fact]
    public void AdvancedRequestedTweaksRemainVisibleAndClearlyUnevaluated()
    {
        Assert.Equal(71, TweakCatalog.Items.Count);
        foreach (var id in new[] { "WPFTweaksDisableBitLocker", "WPFTweaksDisableIPv6", "WPFTweaksTeredo", "TaskbarSize", "TimerPolicies" })
        {
            var tweak = Assert.Single(TweakCatalog.Items, p => p.Id == id);
            Assert.Contains("Requiere evaluación", tweak.Status);
            Assert.Contains("no consultado", tweak.CurrentState);
            Assert.NotEmpty(tweak.Risks);
            Assert.NotEmpty(tweak.ReferenceTargets);
        }
    }

    [Theory]
    [InlineData("ms-settings:taskbar & calc.exe")]
    [InlineData("powershell.exe")]
    [InlineData("https://example.com")]
    public void NativeSettingsOnlyAcceptNamedAllowlistedDestinations(string key)
    {
        Assert.Throws<ArgumentException>(() => WindowsSettingsCatalog.GetUri(key));
    }

    [Fact]
    public void TaskbarAndUpdateShortcutsUseTheExpectedNativePages()
    {
        Assert.Equal("ms-settings:taskbar", WindowsSettingsCatalog.GetUri("Taskbar"));
        Assert.Equal("ms-settings:windowsupdate", WindowsSettingsCatalog.GetUri("Updates"));
        Assert.Equal("ms-settings:windowsupdate-activehours", WindowsSettingsCatalog.GetUri("UpdateHours"));
    }
}
