using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class TweakProfilesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pcoptimizer-profiles-" + Guid.NewGuid());
    [Fact]
    public void SavesOnlySelectionIdsAndReloadsWithoutAnyRegistryBackup()
    {
        var path = Path.Combine(directory, "gaming.json");
        var profile = new SavedTweakProfile(1, "Mi selección", ["game-mode", "long-paths"]);
        TweakProfiles.Save(path, profile);
        Assert.Equal(profile.TweakIds, TweakProfiles.Read(path).TweakIds);
        Assert.DoesNotContain("HKLM", File.ReadAllText(path));
        Assert.DoesNotContain("PreviousValue", File.ReadAllText(path));
    }
    [Fact]
    public void UnknownInstructionsCannotBecomeExecutableProfileEntries()
    {
        Assert.Throws<InvalidDataException>(() => TweakProfiles.Validate(new(1, "Unsafe", ["powershell.exe -Command anything"])));
        Assert.Throws<InvalidDataException>(() => TweakProfiles.Validate(new(1, "Conflict", ["hags", "hags-off"])));
        Assert.Throws<InvalidDataException>(() => TweakProfiles.Validate(new(1, "Duplicates", ["game-mode", "game-mode"])));
    }
    [Fact]
    public void MachineScriptRejectsUserCommandsInsteadOfInterpolatingThem()
    {
        var missing = RegistryValueSnapshot.Missing;
        Assert.Throws<InvalidDataException>(() => MachineTweakScript.Build([new("unknown", "hags", missing, new(true, "DWord", "2"))]));
        Assert.Throws<FormatException>(() => MachineTweakScript.Build([new("hags", "hags", missing, new(true, "DWord", "2; Start-Process something"))]));
        Assert.Throws<InvalidOperationException>(() => MachineTweakScript.Build([new("game-mode", "game-mode-auto", missing, new(true, "DWord", "1"))]));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
