using FlatPatternExporter.Enums;
using FlatPatternExporter.Features;
using FlatPatternExporter.Services;

namespace PatternExporterNX.Tests;

public sealed class ApplicationSettingsFeatureGuardTests
{
    [Fact]
    public void PreserveDisabledFeatures_KeepsFrameSettingsInSheetMetalEdition()
    {
        var savedFrame = new FrameExportSettings { FileNameTemplate = "saved-frame" };
        var collected = new ApplicationSettings
        {
            FrameExport = new FrameExportSettings { FileNameTemplate = "hidden-control-default" }
        };

        var result = ApplicationSettingsFeatureGuard.PreserveDisabledFeatures(
            collected,
            new ApplicationSettings { FrameExport = savedFrame },
            ProductFeatureProfile.For(ProductEdition.SheetMetal));

        Assert.Same(savedFrame, result.FrameExport);
    }

    [Fact]
    public void PreserveDisabledFeatures_KeepsSheetSettingsInFrameEdition()
    {
        var savedDxf = new DxfExportSettings { TopSideMode = FlatPatternTopSideMode.MostBendsDown };
        var savedBendAnnotations = new BendAnnotationSettings { Template = "saved-bend-template" };
        var savedFileName = new FileNameSettings { FileNameTemplate = "saved-sheet" };
        var saved = new ApplicationSettings
        {
            DxfExport = savedDxf,
            BendAnnotations = savedBendAnnotations,
            FileName = savedFileName
        };

        var result = ApplicationSettingsFeatureGuard.PreserveDisabledFeatures(
            new ApplicationSettings(),
            saved,
            ProductFeatureProfile.For(ProductEdition.Frame));

        Assert.Same(savedDxf, result.DxfExport);
        Assert.Same(savedBendAnnotations, result.BendAnnotations);
        Assert.Same(savedFileName, result.FileName);
    }
}
