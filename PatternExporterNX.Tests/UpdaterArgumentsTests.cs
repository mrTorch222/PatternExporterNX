using FlatPatternExporter.Updater;

namespace PatternExporterNX.Tests;

public sealed class UpdaterArgumentsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"PatternExporterNX_Args_{Guid.NewGuid():N}");

    [Fact]
    public void TryParse_AcceptsValidSeparatedPaths()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var targetDirectory = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        var executable = Path.Combine(targetDirectory, "PatternExporterNX.exe");
        File.WriteAllText(executable, "application");

        var success = UpdaterArguments.TryParse(["42", source, executable], out var result, out var error);

        Assert.True(success, error);
        Assert.NotNull(result);
        Assert.Equal(42, result.ProcessId);
        Assert.Equal(Path.GetFullPath(source), result.UpdateFilesPath);
        Assert.Equal(Path.GetFullPath(executable), result.TargetExecutablePath);
    }

    [Fact]
    public void TryParse_RejectsUpdateDirectoryInsideApplicationDirectory()
    {
        var targetDirectory = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        var source = Directory.CreateDirectory(Path.Combine(targetDirectory, "update")).FullName;
        var executable = Path.Combine(targetDirectory, "PatternExporterNX.exe");
        File.WriteAllText(executable, "application");

        var success = UpdaterArguments.TryParse(["42", source, executable], out _, out var error);

        Assert.False(success);
        Assert.Contains("outside", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-pid")]
    public void TryParse_RejectsInvalidProcessId(string processId)
    {
        var success = UpdaterArguments.TryParse([processId, "missing", "missing.exe"], out _, out var error);

        Assert.False(success);
        Assert.Contains("process ID", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_RejectsMalformedPathWithoutThrowing()
    {
        var success = UpdaterArguments.TryParse(["42", "invalid\0path", "target.exe"], out _, out var error);

        Assert.False(success);
        Assert.Contains("path", error, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
