using System.Globalization;
using System.IO;
using FlatPatternExporter.Enums;
using netDxf;

namespace FlatPatternExporter.Utilities;

public sealed record DxfPostProcessOptions
{
    public bool OptimizeVersion { get; init; }
    public AcadVersionType TargetVersion { get; init; }
}

public sealed record DxfPostProcessResult(string FilePath);

public static class DxfPostProcessor
{
    public static DxfPostProcessResult Process(string filePath, DxfPostProcessOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"DXF path has no directory: '{fullPath}'.");
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var document = DxfDocument.Load(fullPath)
                ?? throw new InvalidDataException($"netDxf could not load '{fullPath}'.");

            if (options.OptimizeVersion)
            {
                var version = AcadVersionMapping.GetDxfVersion(options.TargetVersion)
                    ?? throw new NotSupportedException($"AutoCAD {AcadVersionMapping.GetDisplayName(options.TargetVersion)} cannot be saved by netDxf.");
                document.DrawingVariables.AcadVer = version;
            }

            if (!document.Save(tempPath))
                throw new IOException($"netDxf could not save temporary file '{tempPath}'.");

            File.Move(tempPath, fullPath, true);
            return new DxfPostProcessResult(fullPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"DXF post-processing failed for '{fullPath}': {ex.Message}"), ex);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
