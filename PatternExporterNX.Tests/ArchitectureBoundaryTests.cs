namespace PatternExporterNX.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void SharedAndFeatureBackendCodeDoesNotReferenceUiImplementation()
    {
        string root = FindRepositoryRoot();
        string[] directories =
        [
            "FlatPatternExporter/Core",
            "FlatPatternExporter/Models",
            "FlatPatternExporter/Services",
            "FlatPatternExporter/Utilities",
            "FlatPatternExporter/Features/Frame/Core",
            "FlatPatternExporter/Features/Frame/Models",
            "FlatPatternExporter/Features/Frame/Services",
            "FlatPatternExporter/Features/SheetMetal/Core",
            "FlatPatternExporter/Features/SheetMetal/Models",
            "FlatPatternExporter/Features/SheetMetal/Utilities"
        ];

        AssertReferencesAbsent(root, directories, "FlatPatternExporter.UI.");
    }

    [Fact]
    public void SharedCodeDoesNotDependOnFeatureImplementations()
    {
        string root = FindRepositoryRoot();
        string[] directories =
        [
            "FlatPatternExporter/Core",
            "FlatPatternExporter/Models",
            "FlatPatternExporter/Services",
            "FlatPatternExporter/Utilities"
        ];

        AssertReferencesAbsent(root, directories, "FlatPatternExporter.Features.Frame.");
        AssertReferencesAbsent(root, directories, "FlatPatternExporter.Features.SheetMetal.");
    }

    [Fact]
    public void FrameAndSheetMetalFeaturesDoNotReferenceEachOther()
    {
        string root = FindRepositoryRoot();
        AssertReferencesAbsent(root, ["FlatPatternExporter/Features/Frame"], "FlatPatternExporter.Features.SheetMetal.");
        AssertReferencesAbsent(root, ["FlatPatternExporter/Features/SheetMetal"], "FlatPatternExporter.Features.Frame.");
    }

    private static void AssertReferencesAbsent(string root, IEnumerable<string> directories, string forbiddenReference)
    {
        var violations = new List<string>();

        foreach (string relativeDirectory in directories)
        {
            string directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory)) continue;

            foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (File.ReadAllText(file).Contains(forbiddenReference, StringComparison.Ordinal))
                    violations.Add(Path.GetRelativePath(root, file));
            }
        }

        Assert.True(violations.Count == 0, $"Forbidden reference '{forbiddenReference}': {string.Join(", ", violations)}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PatternExporterNX.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
