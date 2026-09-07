using FlatPatternExporter.Utilities;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;
using netDxf.Units;

namespace PatternExporterNX.Tests;

public sealed class DxfCutLengthCalculatorTests
{
    private static readonly string FixtureDirectory = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "TestData", "Baseline", "Inventor2027"));

    [Theory]
    [InlineData("rectangle-hole-mm.dxf")]
    [InlineData("rectangle-hole-m.dxf")]
    public void CalculateMillimeters_UsesDxfUnits(string fileName)
    {
        var document = DxfDocument.Load(Path.Combine(FixtureDirectory, fileName))!;

        var length = DxfCutLengthCalculator.CalculateMillimeters(
            document,
            new HashSet<string> { "IV_OUTER_PROFILE", "IV_INTERIOR_PROFILES" },
            DocumentLengthUnit.Unknown);

        Assert.NotNull(length);
        Assert.InRange(length.Value, 362.8318, 362.8319);
    }

    [Fact]
    public void CalculateMillimeters_SumsSupportedGeometryAndExcludesOtherLayers()
    {
        var document = new DxfDocument();
        document.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
        var cutting = new Layer("CUT");
        var ignored = new Layer("BEND");
        document.Entities.Add(new Line(new Vector3(0, 0, 0), new Vector3(10, 0, 0)) { Layer = cutting });
        document.Entities.Add(new Arc(Vector3.Zero, 5, 0, 180) { Layer = cutting });
        document.Entities.Add(new Circle(Vector3.Zero, 2) { Layer = cutting });
        document.Entities.Add(new Ellipse(Vector3.Zero, 10, 10) { Layer = cutting });
        document.Entities.Add(new Polyline2D(
            [new Polyline2DVertex(0, 0), new Polyline2DVertex(3, 0), new Polyline2DVertex(3, 4)], false)
            { Layer = cutting });
        document.Entities.Add(new Spline([new Vector3(0, 0, 0), new Vector3(10, 0, 0)]) { Layer = cutting });
        document.Entities.Add(new Line(Vector3.Zero, new Vector3(1000, 0, 0)) { Layer = ignored });

        var length = DxfCutLengthCalculator.CalculateMillimeters(
            document, new HashSet<string> { "CUT" }, DocumentLengthUnit.Unknown);

        var expected = 10 + 5 * Math.PI + 4 * Math.PI + 10 * Math.PI + 7 + 10;
        Assert.NotNull(length);
        Assert.InRange(length.Value, expected - 0.01, expected + 0.01);
    }

    [Fact]
    public void CalculateMillimeters_DoesNotAssumeMillimetersForUnitlessDocument()
    {
        var document = new DxfDocument();
        document.DrawingVariables.InsUnits = DrawingUnits.Unitless;
        document.Entities.Add(new Line(Vector3.Zero, new Vector3(10, 0, 0)) { Layer = new Layer("CUT") });

        Assert.Null(DxfCutLengthCalculator.CalculateMillimeters(
            document, new HashSet<string> { "CUT" }, DocumentLengthUnit.Unknown));
    }
}
