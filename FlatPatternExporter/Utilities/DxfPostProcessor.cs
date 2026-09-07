using System.Globalization;
using System.IO;
using FlatPatternExporter.Enums;
using netDxf;
using netDxf.Entities;

namespace FlatPatternExporter.Utilities;

public sealed record DxfPostProcessOptions
{
    public bool OptimizeVersion { get; init; }
    public AcadVersionType TargetVersion { get; init; }
    public bool ConvertSplinesToFitPoints { get; init; }
    public double SplineTolerance { get; init; } = 0.01;
    public IReadOnlySet<string> CuttingLayers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public DocumentLengthUnit DocumentUnit { get; init; } = DocumentLengthUnit.Unknown;
}

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
