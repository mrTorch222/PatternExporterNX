using FlatPatternExporter.Updater;

namespace PatternExporterNX.Tests;

public sealed class UpdateTransactionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"PatternExporterNX_Update_{Guid.NewGuid():N}");

    [Fact]
    public void ApplyAndRollback_RestoresOnlyManagedFiles()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        var executable = Write(target, "PatternExporterNX.exe", "old executable");
        Write(target, "UserData/custom.txt", "keep me");
        Write(source, "PatternExporterNX.exe", "new executable");
        Write(source, "Libraries/feature.dll", "new library");

        using var transaction = new UpdateTransaction(source, executable, maxCopyAttempts: 1);
        transaction.CreateBackup();
        var backupDirectory = transaction.BackupDirectory;

        transaction.Apply();

        Assert.Equal("new executable", File.ReadAllText(executable));
        Assert.Equal("new library", File.ReadAllText(Path.Combine(target, "Libraries", "feature.dll")));
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(target, "UserData", "custom.txt")));

        transaction.Rollback();

        Assert.Equal("old executable", File.ReadAllText(executable));
        Assert.False(File.Exists(Path.Combine(target, "Libraries", "feature.dll")));
        Assert.Equal("keep me", File.ReadAllText(Path.Combine(target, "UserData", "custom.txt")));

        transaction.Cleanup();
        Assert.False(Directory.Exists(backupDirectory));
    }

    [Fact]
    public void FailedApply_CanRollbackPartialReplacement()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        var executable = Write(target, "PatternExporterNX.exe", "old executable");
        var lockedTarget = Write(target, "z-locked.dll", "old locked");
        Write(source, "PatternExporterNX.exe", "new executable");
        Write(source, "z-locked.dll", "new locked");

        using var transaction = new UpdateTransaction(source, executable, maxCopyAttempts: 1);
        transaction.CreateBackup();

        using (File.Open(lockedTarget, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(transaction.Apply);
        }

        transaction.Rollback();

        Assert.Equal("old executable", File.ReadAllText(executable));
        Assert.Equal("old locked", File.ReadAllText(lockedTarget));
    }

    [Fact]
    public void Constructor_RejectsEmptyUpdateDirectory()
    {
        var source = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        var executable = Write(target, "PatternExporterNX.exe", "old executable");

        var exception = Assert.Throws<InvalidOperationException>(() => new UpdateTransaction(source, executable));

        Assert.Contains("no files", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string Write(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
