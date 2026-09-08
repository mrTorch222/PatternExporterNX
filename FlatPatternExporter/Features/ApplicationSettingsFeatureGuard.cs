using FlatPatternExporter.Services;

namespace FlatPatternExporter.Features;

public static class ApplicationSettingsFeatureGuard
{
    public static ApplicationSettings PreserveDisabledFeatures(
        ApplicationSettings collected,
        ApplicationSettings saved,
        ProductFeatureProfile profile)
    {
        if (!profile.FrameEnabled)
            collected = collected with { FrameExport = saved.FrameExport };

        if (!profile.SheetMetalEnabled)
        {
            collected = collected with
            {
                ComponentFilter = saved.ComponentFilter,
                Organization = saved.Organization,
                Hierarchy = saved.Hierarchy,
                SelectedProcessingMethod = saved.SelectedProcessingMethod,
                DxfExport = saved.DxfExport,
                BendAnnotations = saved.BendAnnotations,
                Spline = saved.Spline,
                ExportFolder = saved.ExportFolder,
                FileName = saved.FileName,
                ExcelExport = saved.ExcelExport,
                LayerSettings = saved.LayerSettings
            };
        }

        return collected;
    }
}
