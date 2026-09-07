using netDxf;
using netDxf.Entities;
using netDxf.Units;

namespace FlatPatternExporter.Utilities;

public static class DxfCutLengthCalculator
{
    public static double? CalculateMillimeters(
        DxfDocument document,
        IReadOnlySet<string> cuttingLayers,
        DocumentLengthUnit documentUnit,
        double tolerance = 0.001)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(cuttingLayers);
        if (cuttingLayers.Count == 0) return 0.0;

        var drawingUnit = FromDrawingUnits(document.DrawingVariables.InsUnits);
        var sourceUnit = drawingUnit != DocumentLengthUnit.Unknown ? drawingUnit : documentUnit;
        var multiplier = LengthUnitConverter.ToMillimeters(1.0, sourceUnit);
        if (multiplier is null) return null;

        var total = document.Entities.Lines.Where(IsCutting).Sum(line => Vector3.Distance(line.StartPoint, line.EndPoint));
        total += document.Entities.Arcs.Where(IsCutting).Sum(arc => arc.Radius * SweepRadians(arc.StartAngle, arc.EndAngle));
        total += document.Entities.Circles.Where(IsCutting).Sum(circle => 2.0 * Math.PI * circle.Radius);
        total += document.Entities.Ellipses.Where(IsCutting).Sum(ellipse => AdaptiveLength(ellipse.PolygonalVertexes, ellipse.IsFullEllipse, tolerance));
        total += document.Entities.Polylines2D.Where(IsCutting).Sum(Polyline2DLength);
        total += document.Entities.Polylines3D.Where(IsCutting).Sum(polyline => PolylineLength(polyline.Vertexes, polyline.IsClosed));
        total += document.Entities.Splines.Where(IsCutting).Sum(spline => AdaptiveLength(spline.PolygonalVertexes, spline.IsClosed, tolerance));
        return total * multiplier.Value;

        bool IsCutting(EntityObject entity) => cuttingLayers.Contains(entity.Layer.Name);
    }

    public static DocumentLengthUnit FromDrawingUnits(DrawingUnits units) => units switch
    {
        DrawingUnits.Millimeters => DocumentLengthUnit.Millimeter,
        DrawingUnits.Meters => DocumentLengthUnit.Meter,
        DrawingUnits.Centimeters => DocumentLengthUnit.Centimeter,
        DrawingUnits.Inches => DocumentLengthUnit.Inch,
        DrawingUnits.Feet => DocumentLengthUnit.Foot,
        _ => DocumentLengthUnit.Unknown
    };

    private static double Polyline2DLength(Polyline2D polyline)
    {
        if (polyline.Vertexes.Count < 2) return 0.0;
        var segmentCount = polyline.IsClosed ? polyline.Vertexes.Count : polyline.Vertexes.Count - 1;
        var length = 0.0;
        for (var index = 0; index < segmentCount; index++)
        {
            var start = polyline.Vertexes[index];
            var end = polyline.Vertexes[(index + 1) % polyline.Vertexes.Count];
            var chord = Vector2.Distance(start.Position, end.Position);
            var angle = 4.0 * Math.Atan(Math.Abs(start.Bulge));
            length += angle <= 1.0e-12 ? chord : chord * angle / (2.0 * Math.Sin(angle / 2.0));
        }
        return length;
    }

    private static double PolylineLength(IReadOnlyList<Vector3> points, bool closed)
    {
        if (points.Count < 2) return 0.0;
        var length = 0.0;
        for (var index = 1; index < points.Count; index++)
            length += Vector3.Distance(points[index - 1], points[index]);
        if (closed) length += Vector3.Distance(points[^1], points[0]);
        return length;
    }

    private static double AdaptiveLength(Func<int, List<Vector3>> polygonalVertexes, bool closed, double tolerance)
    {
        var previous = 0.0;
        for (var precision = 32; precision <= 32768; precision *= 2)
        {
            var current = PolylineLength(polygonalVertexes(precision), closed);
            if (precision > 32 && Math.Abs(current - previous) <= tolerance) return current;
            previous = current;
        }
        return previous;
    }

    private static double AdaptiveLength(Func<int, List<Vector2>> polygonalVertexes, bool closed, double tolerance)
    {
        return AdaptiveLength(
            precision => polygonalVertexes(precision).Select(point => new Vector3(point.X, point.Y, 0)).ToList(),
            closed,
            tolerance);
    }

    private static double SweepRadians(double startAngle, double endAngle)
    {
        var sweep = (endAngle - startAngle) % 360.0;
        if (sweep < 0) sweep += 360.0;
        return sweep * Math.PI / 180.0;
    }
}
