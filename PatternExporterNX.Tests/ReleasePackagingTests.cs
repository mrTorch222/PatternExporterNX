namespace PatternExporterNX.Tests;

public sealed class ReleasePackagingTests
{
    [Fact]
    public void ReleaseDraftRequiresAndUploadsVersionMatchedUpdater()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "create-release-draft.bat"));
        const string updaterArchive = "PatternExporterNX.Updater-v%VERSION%-x64.zip";

        Assert.Contains($"if not exist \"Release\\{updaterArchive}\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"Release\\PatternExporterNX-v%VERSION%-x64-*.zip Release\\{updaterArchive}",
            script,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--notes-file \"%NOTES_FILE%\" Release\\*.zip", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublishScriptSupportsUpdaterCommandLineProfile()
    {
        var script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "publish.bat"));

        Assert.Contains("if \"%profile%\"==\"updater\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("call :PublishUpdaterPortable", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("git rev-list --count HEAD", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("if errorlevel 1 goto :eof", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("findstr /C:\"File Version:\"", script, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PatternExporterNX.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
