using FlatPatternExporter.Enums;
using FlatPatternExporter.Utilities;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace PatternExporterNX.Tests;

public sealed class DxfPostProcessorTests
{
    private static readonly string Fixture = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "TestData", "Baseline", "Inventor2027", "rectangle-hole-mm.dxf"));
    private static readonly string SplineFixture = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "TestData", "Baseline", "Inventor2027", "spline-control-mm.dxf"));

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

    [Fact]
    public void Process_ConvertsControlPointSplineToFitPointSplineWithinTolerance()
    {
        const double tolerance = 0.01;
        var path = CopyFixture(SplineFixture);
        var before = DxfDocument.Load(path)!;
        var source = Assert.Single(before.Entities.Splines);
        var sourceLayer = source.Layer.Name;
        var sourceColor = source.Color;

        var result = DxfPostProcessor.Process(path, new DxfPostProcessOptions
        {
            ConvertSplinesToFitPoints = true,
            SplineTolerance = tolerance
        });

        var after = DxfDocument.Load(path)!;
        var converted = Assert.Single(after.Entities.Splines);
        Assert.NotEmpty(converted.FitPoints);
        Assert.Equal(sourceLayer, converted.Layer.Name);
        Assert.Equal(sourceColor, converted.Color);
        Assert.Equal(1, result.ConvertedSplineCount);
        Assert.InRange(result.MaximumSplineDeviation, 0, tolerance);
    }

    [Fact]
    public void Process_ConvertsPeriodicSplineWithoutOpeningCurve()
    {
        var path = Path.Combine(Path.GetTempPath(), $"periodic-{Guid.NewGuid():N}.dxf");
        var document = new DxfDocument();
        document.Entities.Add(new Spline(
            [
                new Vector3(0, 0, 0),
                new Vector3(10, 0, 0),
                new Vector3(10, 10, 0),
                new Vector3(0, 10, 0)
            ], null, 3, true));
        Assert.True(document.Save(path));

        DxfPostProcessor.Process(path, new DxfPostProcessOptions
        {
            ConvertSplinesToFitPoints = true,
            SplineTolerance = 0.05
        });

        var converted = Assert.Single(DxfDocument.Load(path)!.Entities.Splines);
        Assert.NotEmpty(converted.FitPoints);
        Assert.True(converted.IsClosed);
    }

    [Fact]
    public void Process_AddsConfiguredTextToMatchingBendLine()
    {
        var path = CreateBendLineFixture();

        DxfPostProcessor.Process(path, new DxfPostProcessOptions
        {
            BendAnnotations = new BendAnnotationRenderOptions(
                "{Direction} {Angle} R{Radius} L{Length}",
                "Arial",
                3,
                false,
                "BEND_TEXT",
                "Red",
                "IV_BEND",
                "IV_BEND_DOWN",
                [new BendAnnotationSource(20, 90, 2.5, true)])
        });

        var text = Assert.Single(DxfDocument.Load(path)!.Entities.Texts);
        Assert.Equal("UP 90 R2.5 L20", text.Value);
        Assert.Equal("BEND_TEXT", text.Layer.Name);
        Assert.Equal(3, text.Height);
    }

    [Fact]
    public void Process_ConvertsBendTextToClosedPolylines()
    {
        var path = CreateBendLineFixture();

        DxfPostProcessor.Process(path, new DxfPostProcessOptions
        {
            BendAnnotations = new BendAnnotationRenderOptions(
                "R{Radius}",
                "Arial",
                3,
                true,
                "BEND_TEXT",
                "White",
                "IV_BEND",
                "IV_BEND_DOWN",
                [new BendAnnotationSource(20, 90, 2.5, true)])
        });

        var document = DxfDocument.Load(path)!;
        Assert.Empty(document.Entities.Texts);
        Assert.NotEmpty(document.Entities.Polylines2D);
        Assert.All(document.Entities.Polylines2D, polyline =>
        {
            Assert.Equal("BEND_TEXT", polyline.Layer.Name);
            Assert.True(polyline.IsClosed);
        });
    }

    private static string CreateBendLineFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bend-annotation-{Guid.NewGuid():N}.dxf");
        var document = new DxfDocument();
        document.Entities.Add(new Line(Vector3.Zero, new Vector3(20, 0, 0))
        {
            Layer = new Layer("IV_BEND")
        });
        Assert.True(document.Save(path));
        return path;
    }

    private static string CopyFixture(string? fixture = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"post-process-{Guid.NewGuid():N}.dxf");
        File.Copy(fixture ?? Fixture, path);
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
