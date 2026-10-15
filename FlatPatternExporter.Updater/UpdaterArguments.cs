using System.IO;

namespace FlatPatternExporter.Updater;

public sealed record UpdaterArguments(int ProcessId, string UpdateFilesPath, string TargetExecutablePath)
{
    public static bool TryParse(string[] args, out UpdaterArguments? result, out string error)
    {
        result = null;

        if (args.Length != 3)
        {
            error = "Usage: PatternExporterNX.Updater.exe <ProcessId> <UpdateFilesDirectory> <TargetExecutablePath>";
            return false;
        }

        if (!int.TryParse(args[0], out var processId) || processId <= 0)
        {
            error = "Invalid process ID.";
            return false;
        }

        string updateFilesPath;
        string targetExecutablePath;
        try
        {
            updateFilesPath = Path.GetFullPath(args[1]);
            targetExecutablePath = Path.GetFullPath(args[2]);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Invalid update path: {exception.Message}";
            return false;
        }

        if (!Directory.Exists(updateFilesPath))
        {
            error = $"Update directory not found:{Environment.NewLine}{updateFilesPath}";
            return false;
        }

        if (!File.Exists(targetExecutablePath))
        {
            error = $"Target executable not found:{Environment.NewLine}{targetExecutablePath}";
            return false;
        }

        var targetDirectory = Path.GetDirectoryName(targetExecutablePath)!;
        if (PathsOverlap(updateFilesPath, targetDirectory))
        {
            error = "Update files must be stored outside the application directory.";
            return false;
        }

        result = new UpdaterArguments(processId, updateFilesPath, targetExecutablePath);
        error = string.Empty;
        return true;
    }

    private static bool PathsOverlap(string first, string second)
    {
        var firstPath = EnsureTrailingSeparator(Path.GetFullPath(first));
        var secondPath = EnsureTrailingSeparator(Path.GetFullPath(second));

        return firstPath.StartsWith(secondPath, StringComparison.OrdinalIgnoreCase) ||
               secondPath.StartsWith(firstPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
