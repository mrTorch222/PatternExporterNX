using System.IO;

namespace FlatPatternExporter.Features.Frame.Services;

public static class FrameMemberIdentity
{
    public static string Create(string? fullFileName, string? displayName, string? modelState)
    {
        var documentIdentity = string.IsNullOrWhiteSpace(fullFileName)
            ? $"display:{displayName?.Trim()}"
            : Path.GetFullPath(fullFileName).TrimEnd(Path.DirectorySeparatorChar);
        return $"{documentIdentity}|model-state:{modelState?.Trim()}";
    }
}
