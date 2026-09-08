using System.Text.Json;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Services;

namespace PatternExporterNX.Tests;

public sealed class SettingsCompatibilityTests
{
    [Fact]
    public void SavedFrameExportSelectionsSurviveRoundTrip()
    {
        const string json = """
            { "FrameExport": { "GeometryType": 1, "SolidFaceType": 0, "SurfaceType": 0 } }
            """;
        var settings = JsonSerializer.Deserialize<ApplicationSettings>(json)!;
        var restored = JsonSerializer.Deserialize<ApplicationSettings>(JsonSerializer.Serialize(settings))!;

        Assert.Equal(1, restored.FrameExport.GeometryType);
        Assert.Equal(0, restored.FrameExport.SolidFaceType);
        Assert.Equal(0, restored.FrameExport.SurfaceType);
    }

    [Fact]
    public void LegacySettingsWithoutNewValuesKeepDefaults()
    {
        const string legacyJson = """
            {
              "Spline": {
                "EnableSplineReplacement": true,
                "SelectedSplineReplacement": 1,
                "SplineTolerance": "0.02"
              }
            }
            """;

        var settings = JsonSerializer.Deserialize<ApplicationSettings>(legacyJson)!;

        Assert.Equal(SplineReplacementType.Arcs, settings.Spline.SelectedSplineReplacement);
        Assert.Equal("0.02", settings.Spline.SplineTolerance);
        Assert.NotNull(settings.Interface);
        Assert.NotNull(settings.FrameExport);
        Assert.Equal(FrameFileNameService.DefaultTemplate, settings.FrameExport.FileNameTemplate);
        Assert.Equal(0, settings.FrameExport.GeometryType);
        Assert.Equal(1, settings.FrameExport.SolidFaceType);
        Assert.Equal(1, settings.FrameExport.SurfaceType);
        Assert.Equal(0, (int)SplineReplacementType.Lines);
        Assert.Equal(1, (int)SplineReplacementType.Arcs);
        Assert.Equal(2, (int)SplineReplacementType.FitPoints);
    }
}
