using System.Globalization;
using System.IO;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

namespace FlatPatternExporter.Utilities;

public sealed record DxfPostProcessOptions
{
    public bool OptimizeVersion { get; init; }
    public AcadVersionType TargetVersion { get; init; }
    public bool ConvertSplinesToFitPoints { get; init; }
    public double SplineTolerance { get; init; } = 0.01;
    public IReadOnlySet<string> CuttingLayers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public DocumentLengthUnit DocumentUnit { get; init; } = DocumentLengthUnit.Unknown;
    public BendAnnotationRenderOptions? BendAnnotations { get; init; }
}

public sealed record BendAnnotationSource(double Length, double AngleDegrees, double Radius, bool IsUp);

public sealed record BendAnnotationRenderOptions(
    string Template,
    string FontFamily,
    double TextHeight,
    bool ConvertToCurves,
    string LayerName,
    string ColorName,
    string BendUpLayerName,
    string BendDownLayerName,
    IReadOnlyList<BendAnnotationSource> Sources);

public sealed record DxfPostProcessResult(
    string FilePath,
    int ConvertedSplineCount,
    double MaximumSplineDeviation,
    double? CutLengthMm);

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

            var convertedSplineCount = 0;
            var maximumSplineDeviation = 0.0;
            if (options.ConvertSplinesToFitPoints)
            {
                if (!double.IsFinite(options.SplineTolerance) || options.SplineTolerance <= 0)
                    throw new ArgumentOutOfRangeException(nameof(options.SplineTolerance), "Spline tolerance must be a finite positive number.");

                foreach (var source in document.Entities.Splines
                             .Where(spline => spline.FitPoints.Count == 0)
                             .ToList())
                {
                    try
                    {
                        var conversion = ConvertSpline(source, options.SplineTolerance);
                        document.Entities.Remove(source);
                        document.Entities.Add(conversion.Spline);
                        convertedSplineCount++;
                        maximumSplineDeviation = Math.Max(maximumSplineDeviation, conversion.MaximumDeviation);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException(
                            $"Cannot convert SPLINE on layer '{source.Layer.Name}' in '{fullPath}': {ex.Message}", ex);
                    }
                }
            }

            if (options.OptimizeVersion)
            {
                var version = AcadVersionMapping.GetDxfVersion(options.TargetVersion)
                    ?? throw new NotSupportedException($"AutoCAD {AcadVersionMapping.GetDisplayName(options.TargetVersion)} cannot be saved by netDxf.");
                document.DrawingVariables.AcadVer = version;
            }

            if (options.BendAnnotations is { TextHeight: > 0 } bendOptions)
                AddBendAnnotations(document, bendOptions);

            var cutLengthMm = DxfCutLengthCalculator.CalculateMillimeters(
                document, options.CuttingLayers, options.DocumentUnit);

            if (!document.Save(tempPath))
                throw new IOException($"netDxf could not save temporary file '{tempPath}'.");

            File.Move(tempPath, fullPath, true);
            return new DxfPostProcessResult(fullPath, convertedSplineCount, maximumSplineDeviation, cutLengthMm);
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

    private static void AddBendAnnotations(DxfDocument document, BendAnnotationRenderOptions options)
    {
        var layer = document.Layers.FirstOrDefault(item =>
            string.Equals(item.Name, options.LayerName, StringComparison.OrdinalIgnoreCase));
        if (layer is null)
        {
            layer = new Layer(options.LayerName);
            document.Layers.Add(layer);
        }
        layer.Color = ParseColor(options.ColorName);

        TextStyle? textStyle = null;
        if (!options.ConvertToCurves)
        {
            textStyle = document.TextStyles.FirstOrDefault(item =>
                string.Equals(item.Name, "PATTERN_EXPORTER_BEND", StringComparison.OrdinalIgnoreCase));
            if (textStyle is null)
            {
                textStyle = new TextStyle(
                    "PATTERN_EXPORTER_BEND",
                    options.FontFamily,
                    netDxf.Tables.FontStyle.Regular);
                document.TextStyles.Add(textStyle);
            }
        }

        var candidates = document.Entities.Lines
            .Where(line => string.Equals(line.Layer.Name, options.BendUpLayerName, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(line.Layer.Name, options.BendDownLayerName, StringComparison.OrdinalIgnoreCase))
            .Select(line => new
            {
                Line = line,
                IsUp = string.Equals(line.Layer.Name, options.BendUpLayerName, StringComparison.OrdinalIgnoreCase),
                Length = Vector3.Distance(line.StartPoint, line.EndPoint)
            })
            .ToList();

        var remaining = options.Sources.ToList();
        foreach (var candidate in candidates)
        {
            var source = remaining
                .Where(item => item.IsUp == candidate.IsUp)
                .OrderBy(item => Math.Abs(item.Length - candidate.Length))
                .FirstOrDefault();
            if (source is null) continue;
            remaining.Remove(source);

            var text = FormatBendAnnotation(options.Template, source);
            if (string.IsNullOrWhiteSpace(text)) continue;
            var midpoint = (candidate.Line.StartPoint + candidate.Line.EndPoint) * 0.5;
            var rotation = NormalizeTextRotation(Math.Atan2(
                candidate.Line.EndPoint.Y - candidate.Line.StartPoint.Y,
                candidate.Line.EndPoint.X - candidate.Line.StartPoint.X) * 180.0 / Math.PI);
            var radians = rotation * Math.PI / 180.0;
            var position = new Vector2(
                midpoint.X - Math.Sin(radians) * options.TextHeight * 0.7,
                midpoint.Y + Math.Cos(radians) * options.TextHeight * 0.7);

            if (options.ConvertToCurves)
            {
                foreach (var outline in BendTextOutlineConverter.Create(
                             text, options.FontFamily, options.TextHeight, position, rotation, layer))
                    document.Entities.Add(outline);
            }
            else
            {
                document.Entities.Add(new Text(text, position, options.TextHeight, textStyle!)
                {
                    Alignment = TextAlignment.MiddleCenter,
                    Rotation = rotation,
                    Layer = layer
                });
            }
        }
    }

    internal static string FormatBendAnnotation(string template, BendAnnotationSource source)
    {
        var direction = source.IsUp ? "UP" : "DOWN";
        return template
            .Replace("{Direction}", direction, StringComparison.OrdinalIgnoreCase)
            .Replace("{Angle}", source.AngleDegrees.ToString("0.#", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{Radius}", source.Radius.ToString("0.##", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{Length}", source.Length.ToString("0.##", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    private static double NormalizeTextRotation(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle <= -180) angle += 360;
        if (angle > 90) angle -= 180;
        if (angle < -90) angle += 180;
        return angle;
    }

    private static AciColor ParseColor(string colorName)
    {
        var rgb = LayerSettingsHelper.GetColorValue(colorName)
            .Split(';')
            .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
        return AciColor.FromTrueColor((rgb[0] << 16) | (rgb[1] << 8) | rgb[2]);
    }

    private static SplineConversion ConvertSpline(Spline source, double tolerance)
    {
        if (source.ControlPoints.Count() < 2)
            throw new InvalidDataException("the spline has fewer than two control points.");

        for (var fitPointCount = 8; fitPointCount <= 1024; fitPointCount *= 2)
        {
            var fitPoints = source.PolygonalVertexes(fitPointCount).ToList();
            if (fitPoints.Count < 2 || fitPoints.Any(point => !IsFinite(point)))
                throw new InvalidDataException("the evaluated curve contains too few points or non-finite coordinates.");

            if ((source.IsClosed || source.IsClosedPeriodic) && !AreEqual(fitPoints[0], fitPoints[^1]))
                fitPoints.Add(fitPoints[0]);

            var replacement = new Spline(fitPoints)
            {
                Layer = source.Layer,
                Color = source.Color,
                Linetype = source.Linetype,
                Lineweight = source.Lineweight,
                LinetypeScale = source.LinetypeScale,
                Transparency = source.Transparency,
                Normal = source.Normal,
                IsVisible = source.IsVisible,
                FitTolerance = tolerance,
                StartTangent = source.StartTangent,
                EndTangent = source.EndTangent
            };

            foreach (var xData in source.XData.Values)
                replacement.XData.Add((XData)xData.Clone());

            var validationCount = Math.Min(4096, Math.Max(256, fitPointCount * 4));
            var sourcePoints = source.PolygonalVertexes(validationCount).ToList();
            var replacementPoints = replacement.PolygonalVertexes(validationCount).ToList();
            var maximumDeviation = Math.Max(
                MaximumDistanceToPolyline(sourcePoints, replacementPoints, source.IsClosed),
                MaximumDistanceToPolyline(replacementPoints, sourcePoints, replacement.IsClosed));

            if (maximumDeviation <= tolerance)
                return new SplineConversion(replacement, maximumDeviation);
        }

        throw new InvalidDataException($"the fitted curve does not meet tolerance {tolerance:R} after 1024 fit points.");
    }

    private static double MaximumDistanceToPolyline(
        IReadOnlyList<Vector3> points,
        IReadOnlyList<Vector3> polyline,
        bool closed)
    {
        if (polyline.Count < 2) return double.PositiveInfinity;
        var maximum = 0.0;
        foreach (var point in points)
        {
            var minimum = double.PositiveInfinity;
            for (var index = 1; index < polyline.Count; index++)
                minimum = Math.Min(minimum, DistanceToSegment(point, polyline[index - 1], polyline[index]));
            if (closed && !AreEqual(polyline[0], polyline[^1]))
                minimum = Math.Min(minimum, DistanceToSegment(point, polyline[^1], polyline[0]));
            maximum = Math.Max(maximum, minimum);
        }
        return maximum;
    }

    private static double DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        var segment = end - start;
        var lengthSquared = Vector3.DotProduct(segment, segment);
        if (lengthSquared <= double.Epsilon) return Vector3.Distance(point, start);
        var t = Math.Clamp(Vector3.DotProduct(point - start, segment) / lengthSquared, 0.0, 1.0);
        return Vector3.Distance(point, start + t * segment);
    }

    private static bool IsFinite(Vector3 point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static bool AreEqual(Vector3 left, Vector3 right) => Vector3.Distance(left, right) <= 1.0e-9;

    private sealed record SplineConversion(Spline Spline, double MaximumDeviation);
}
