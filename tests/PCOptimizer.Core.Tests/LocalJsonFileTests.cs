using System.Text.Json;
using PCOptimizer.Core;
using Xunit;

namespace PCOptimizer.Core.Tests;

public sealed class LocalJsonFileTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pcoptimizer-json-tests-" + Guid.NewGuid());
    private string PathToFile => Path.Combine(directory, "record.json");

    [Fact]
    public void AtomicWriteCanBeReloadedAndLeavesNoTemporaryFiles()
    {
        LocalJsonFile.Write(PathToFile, new[] { "original" });
        LocalJsonFile.Write(PathToFile, new[] { "updated" });
        Assert.Equal(new[] { "updated" }, LocalJsonFile.Read<string[]>(PathToFile));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void OversizedReadIsRejectedWithoutReadingTheEntirePayloadOrChangingIt()
    {
        Directory.CreateDirectory(directory);
        using (var file = File.Create(PathToFile)) file.SetLength(LocalJsonFile.MaximumBytes + 1);
        Assert.Throws<InvalidDataException>(() => LocalJsonFile.Read<object>(PathToFile));
        Assert.Equal(LocalJsonFile.MaximumBytes + 1, new FileInfo(PathToFile).Length);
    }

    [Fact]
    public void OversizedWritePreservesTheOriginalRecord()
    {
        LocalJsonFile.Write(PathToFile, new[] { "keep" });
        Assert.Throws<InvalidDataException>(() => LocalJsonFile.Write(PathToFile, new string('x', (int)LocalJsonFile.MaximumBytes)));
        Assert.Equal(new[] { "keep" }, LocalJsonFile.Read<string[]>(PathToFile));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void ExcessiveNestingIsRejectedWithoutRewritingTheFile()
    {
        Directory.CreateDirectory(directory);
        var contents = new string('[', 40) + "0" + new string(']', 40);
        File.WriteAllText(PathToFile, contents);
        Assert.Throws<JsonException>(() => LocalJsonFile.Read<object>(PathToFile));
        Assert.Equal(contents, File.ReadAllText(PathToFile));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
