using System.IO.Compression;
using FlatPatternExporter.Services;

namespace PatternExporterNX.Tests;

public sealed class SafeArchiveExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"PatternExporterNX_Zip_{Guid.NewGuid():N}");

    [Fact]
    public void ExtractZip_ExtractsNestedPackage()
    {
        Directory.CreateDirectory(_root);
        var archivePath = Path.Combine(_root, "package.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("Libraries/feature.dll");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("payload");
        }

        var destination = Path.Combine(_root, "destination");
        SafeArchiveExtractor.ExtractZip(archivePath, destination);

        Assert.Equal("payload", File.ReadAllText(Path.Combine(destination, "Libraries", "feature.dll")));
    }

    [Fact]
    public void ExtractZip_RejectsPathTraversal()
    {
        Directory.CreateDirectory(_root);
        var archivePath = Path.Combine(_root, "malicious.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../outside.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("payload");
        }

        var destination = Path.Combine(_root, "destination");

        Assert.Throws<InvalidDataException>(() => SafeArchiveExtractor.ExtractZip(archivePath, destination));
        Assert.False(File.Exists(Path.Combine(_root, "outside.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
