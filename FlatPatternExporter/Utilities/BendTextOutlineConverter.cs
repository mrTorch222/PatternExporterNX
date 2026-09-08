using System.Globalization;
using System.Windows;
using System.Windows.Media;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace FlatPatternExporter.Utilities;

public static class BendTextOutlineConverter
{
    public static IReadOnlyList<Polyline2D> Create(
        string text,
        string fontFamily,
        double height,
        Vector2 position,
        double rotationDegrees,
        Layer layer)
    {
        var typeface = new Typeface(new System.Windows.Media.FontFamily(fontFamily), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            height,
            System.Windows.Media.Brushes.Black,
            1.0);
        var geometry = formatted.BuildGeometry(new System.Windows.Point(0, 0)).GetFlattenedPathGeometry(height / 40.0, ToleranceType.Absolute);
        var bounds = geometry.Bounds;
        var radians = rotationDegrees * Math.PI / 180.0;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        var result = new List<Polyline2D>();

        foreach (var figure in geometry.Figures)
        {
            var points = new List<System.Windows.Point> { figure.StartPoint };
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line:
                        points.Add(line.Point);
                        break;
                    case PolyLineSegment polyline:
                        points.AddRange(polyline.Points);
                        break;
                }
            }
            if (points.Count < 2) continue;

            var vertices = points.Select(point =>
            {
                var x = point.X - bounds.Left - bounds.Width / 2.0;
                var y = -(point.Y - bounds.Top - bounds.Height / 2.0);
                return new Vector2(
                    position.X + x * cosine - y * sine,
                    position.Y + x * sine + y * cosine);
            });
            result.Add(new Polyline2D(vertices, figure.IsClosed) { Layer = layer });
        }

        return result;
    }
}
