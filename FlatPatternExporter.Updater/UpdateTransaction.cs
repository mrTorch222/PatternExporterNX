using System.IO;

namespace FlatPatternExporter.Updater;

public sealed class UpdateTransaction : IDisposable
{
    private readonly string _sourceDirectory;
    private readonly string _targetDirectory;
    private readonly Action<string>? _log;
    private readonly int _maxCopyAttempts;
    private readonly TimeSpan _retryDelay;
    private readonly List<UpdateEntry> _entries = [];
    private bool _backupCreated;
    private bool _preserveBackup;

    public UpdateTransaction(
        string sourceDirectory,
        string targetExecutablePath,
        Action<string>? log = null,
        int maxCopyAttempts = 5,
        TimeSpan? retryDelay = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCopyAttempts, 1);

        _sourceDirectory = Path.GetFullPath(sourceDirectory);
        var targetPath = Path.GetFullPath(targetExecutablePath);
        _targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("Target executable has no parent directory.", nameof(targetExecutablePath));
        _log = log;
        _maxCopyAttempts = maxCopyAttempts;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);

        if (!Directory.Exists(_sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Update directory not found: {_sourceDirectory}");
        }

        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("Target executable not found.", targetPath);
        }

        if (PathsOverlap(_sourceDirectory, _targetDirectory))
        {
            throw new ArgumentException("Update files must be stored outside the application directory.", nameof(sourceDirectory));
        }

        if ((new DirectoryInfo(_sourceDirectory).Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Update directory cannot be a reparse point.");
        }

        BuildManifest();
        if (_entries.Count == 0)
        {
            throw new InvalidOperationException("Update directory contains no files.");
        }
    }

    public string BackupDirectory { get; private set; } = string.Empty;

    public int FileCount => _entries.Count;

    public void CreateBackup()
    {
        if (_backupCreated)
        {
            throw new InvalidOperationException("Backup has already been created.");
        }

        BackupDirectory = Path.Combine(
            Path.GetTempPath(),
            "PatternExporterNX_Backups",
            $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(BackupDirectory);

        foreach (var entry in _entries.Where(entry => entry.ExistedBeforeUpdate))
        {
            var backupPath = GetContainedPath(BackupDirectory, entry.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.Copy(entry.TargetPath, backupPath, overwrite: true);
            _log?.Invoke($"Backed up: {entry.RelativePath}");
        }

        _backupCreated = true;
    }

    public void Apply()
    {
        EnsureBackupCreated();

        foreach (var entry in _entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(entry.TargetPath)!);
            CopyWithRetry(entry.SourcePath, entry.TargetPath);
            _log?.Invoke($"Updated: {entry.RelativePath}");
        }
    }

    public void Rollback()
    {
        EnsureBackupCreated();

        foreach (var entry in _entries.AsEnumerable().Reverse())
        {
            if (entry.ExistedBeforeUpdate)
            {
                var backupPath = GetContainedPath(BackupDirectory, entry.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(entry.TargetPath)!);
                File.Copy(backupPath, entry.TargetPath, overwrite: true);
                _log?.Invoke($"Restored: {entry.RelativePath}");
            }
            else if (File.Exists(entry.TargetPath))
            {
                File.Delete(entry.TargetPath);
                _log?.Invoke($"Removed new file: {entry.RelativePath}");
            }
        }
    }

    public void Cleanup()
    {
        if (!string.IsNullOrEmpty(BackupDirectory) && Directory.Exists(BackupDirectory))
        {
            Directory.Delete(BackupDirectory, recursive: true);
        }

        _backupCreated = false;
        BackupDirectory = string.Empty;
    }

    public void PreserveBackup() => _preserveBackup = true;

    public void Dispose()
    {
        if (!_preserveBackup)
        {
            Cleanup();
        }
    }

    private void BuildManifest()
    {
        foreach (var sourcePath in Directory.EnumerateFiles(_sourceDirectory, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoints(sourcePath);
            var relativePath = Path.GetRelativePath(_sourceDirectory, sourcePath);
            var targetPath = GetContainedPath(_targetDirectory, relativePath);
            _entries.Add(new UpdateEntry(relativePath, sourcePath, targetPath, File.Exists(targetPath)));
        }
    }

    private void CopyWithRetry(string sourcePath, string targetPath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < _maxCopyAttempts)
            {
                _log?.Invoke($"File is locked; retry {attempt}/{_maxCopyAttempts}: {Path.GetFileName(targetPath)}");
                Thread.Sleep(_retryDelay);
            }
        }
    }

    private void RejectReparsePoints(string filePath)
    {
        FileSystemInfo? current = new FileInfo(filePath);
        while (current != null && IsContainedPath(_sourceDirectory, current.FullName))
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"Update package contains a reparse point: {current.FullName}");
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null
            };
        }
    }

    private static string GetContainedPath(string root, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!IsContainedPath(root, fullPath))
        {
            throw new InvalidDataException($"Path escapes the expected directory: {relativePath}");
        }

        return fullPath;
    }

    private static bool IsContainedPath(string root, string path)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsOverlap(string first, string second)
    {
        var firstPath = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var secondPath = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return firstPath.StartsWith(secondPath, StringComparison.OrdinalIgnoreCase) ||
               secondPath.StartsWith(firstPath, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureBackupCreated()
    {
        if (!_backupCreated || !Directory.Exists(BackupDirectory))
        {
            throw new InvalidOperationException("CreateBackup must be called before applying or rolling back an update.");
        }
    }

    private sealed record UpdateEntry(
        string RelativePath,
        string SourcePath,
        string TargetPath,
        bool ExistedBeforeUpdate);
}
