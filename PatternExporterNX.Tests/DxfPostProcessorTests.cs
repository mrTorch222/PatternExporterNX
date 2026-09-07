using FlatPatternExporter.Enums;
using FlatPatternExporter.Utilities;

namespace PatternExporterNX.Tests;

public sealed class DxfPostProcessorTests
{
    private static readonly string Fixture = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "TestData", "Baseline", "Inventor2027", "rectangle-hole-mm.dxf"));

    [Fact]
    public void Process_PreservesHeaderCoordinatesAndEntities()
    {
        var path = CopyFixture();
        var before = ReadGeometrySignature(path);

        DxfPostProcessor.Process(path, new DxfPostProcessOptions());

        Assert.Equal(before, ReadGeometrySignature(path));
    }

    [Fact]
    public void Process_AtomicallyPreservesTargetWhenInputIsInvalid()
    {
        var path = Path.Combine(Path.GetTempPath(), $"invalid-{Guid.NewGuid():N}.dxf");
        const string original = "not a dxf";
        File.WriteAllText(path, original);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DxfPostProcessor.Process(path, new DxfPostProcessOptions()));

        Assert.Contains(path, exception.Message);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.*.tmp"));
    }

    [Fact]
    public void Process_ChangesOnlyRequestedAcadVersion()
    {
        var path = CopyFixture();
        var before = ReadGeometrySignature(path);

        DxfPostProcessor.Process(path, new DxfPostProcessOptions
        {
            OptimizeVersion = true,
            TargetVersion = AcadVersionType.V2018
        });

        Assert.Equal(before, ReadGeometrySignature(path));
        Assert.Contains("AC1032", File.ReadAllText(path));
    }

    private static string CopyFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"post-process-{Guid.NewGuid():N}.dxf");
        File.Copy(Fixture, path);
        return path;
    }

    private static string ReadGeometrySignature(string path)
    {
        var lines = File.ReadAllLines(path);
        var pairs = Enumerable.Range(0, lines.Length / 2)
            .Select(i => (Code: int.Parse(lines[i * 2].Trim()), Value: lines[i * 2 + 1].Trim()))
            .ToList();
        var measurement = ReadHeader(pairs, "$MEASUREMENT");
        var units = ReadHeader(pairs, "$INSUNITS");
        var entityStart = pairs.FindIndex(pair => pair.Code == 2 && pair.Value == "ENTITIES") + 1;
        var entityEnd = pairs.FindIndex(entityStart, pair => pair.Code == 0 && pair.Value == "ENDSEC");
        var entityPairs = pairs[entityStart..entityEnd];
        var entities = entityPairs.Where(pair => pair.Code == 0 && pair.Value is "LINE" or "CIRCLE" or "ARC" or "ELLIPSE" or "LWPOLYLINE" or "POLYLINE" or "SPLINE")
            .GroupBy(pair => pair.Value).OrderBy(group => group.Key).Select(group => $"{group.Key}:{group.Count()}");
        var coordinates = entityPairs.Where(pair => pair.Code is 10 or 11 or 20 or 21 or 30 or 31)
            .Select(pair => double.Parse(pair.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        var bounds = $"{coordinates.Min():R}:{coordinates.Max():R}";
        return $"{measurement}|{units}|{string.Join(',', entities)}|{bounds}";
    }

    private static string ReadHeader(IReadOnlyList<(int Code, string Value)> pairs, string name)
    {
        var index = pairs.ToList().FindIndex(pair => pair.Code == 9 && pair.Value == name);
        return pairs[index + 1].Value;
    }
}
