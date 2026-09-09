using System.Text.Json;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Features.Frame.Services;
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
        Assert.True(settings.FrameExport.EnableFileNameConstructor);
        Assert.Equal(FrameFileNameService.DefaultTemplate, settings.FrameExport.FileNameTemplate);
        Assert.True(settings.Hierarchy.MergeDuplicateFiles);
        Assert.Empty(settings.FrameExport.TemplatePresets);
        Assert.Equal(-1, settings.FrameExport.SelectedTemplatePresetIndex);
        Assert.Equal(0, settings.FrameExport.GeometryType);
        Assert.Equal(1, settings.FrameExport.SolidFaceType);
        Assert.Equal(1, settings.FrameExport.SurfaceType);
        Assert.Equal(FrameExportFormat.Iges, settings.FrameExport.ExportFormat);
        Assert.Null(settings.FrameExport.SelectedExportFolder);
        Assert.Equal(CsvDelimiterType.Tab, settings.FrameExport.CsvDelimiter);
        Assert.Equal(ExportFileFormat.Excel, settings.FrameExport.DefaultBomFormat);
        Assert.Equal(ExcelExportFileNameType.DateTimeFormat, settings.FrameExport.BomFileNameType);
        Assert.Null(settings.FrameExport.AttributeColumnOrder);
        Assert.Equal(FlatPatternTopSideMode.AsModeled, settings.DxfExport.TopSideMode);
        Assert.Equal(AppTheme.Dark, settings.Interface.SelectedTheme);
        Assert.Equal("Arial", settings.BendAnnotations.FontFamily);
        Assert.Equal("3", settings.BendAnnotations.TextHeight);
        Assert.Equal(0, (int)SplineReplacementType.Lines);
        Assert.Equal(1, (int)SplineReplacementType.Arcs);
        Assert.Equal(2, (int)SplineReplacementType.FitPoints);
    }

    [Fact]
    public void NewExportFormatAndTopSideSettingsSurviveRoundTrip()
    {
        var settings = new ApplicationSettings
        {
            SelectedProcessingMethod = ProcessingMethod.Hierarchy,
            Hierarchy = new HierarchySettings { MergeDuplicateFiles = false },
            FrameExport = new FrameExportSettings
            {
                EnableFileNameConstructor = false,
                ExportFormat = FrameExportFormat.Step,
                SelectedExportFolder = ExportFolderType.ProjectFolder,
                EnableSubfolder = true,
                SubfolderName = "3D",
                OrganizeByMaterial = true,
                OrganizeByStockNumber = true,
                CsvDelimiter = CsvDelimiterType.Pipe,
                DefaultBomFormat = ExportFileFormat.Csv,
                BomFileNameType = ExcelExportFileNameType.PartNumber,
                AttributeColumnOrder = ["Material", "PartNumber", "UDP_Workshop"],
                TemplatePresets = [new TemplatePresetData { Name = "Workshop", Template = "{StockNumber}_{Length}" }],
                SelectedTemplatePresetIndex = 0
            },
            DxfExport = new DxfExportSettings { TopSideMode = FlatPatternTopSideMode.MostBendsUp },
            BendAnnotations = new BendAnnotationSettings
            {
                Template = "{Angle} / {Radius}",
                FontFamily = "Tahoma",
                TextHeight = "4.5",
                ConvertToCurves = true
            },
            Interface = new InterfaceSettings { SelectedTheme = AppTheme.Ocean }
        };

        var restored = JsonSerializer.Deserialize<ApplicationSettings>(JsonSerializer.Serialize(settings))!;

        Assert.Equal(FrameExportFormat.Step, restored.FrameExport.ExportFormat);
        Assert.False(restored.FrameExport.EnableFileNameConstructor);
        Assert.Equal(ExportFolderType.ProjectFolder, restored.FrameExport.SelectedExportFolder);
        Assert.True(restored.FrameExport.EnableSubfolder);
        Assert.Equal("3D", restored.FrameExport.SubfolderName);
        Assert.True(restored.FrameExport.OrganizeByMaterial);
        Assert.True(restored.FrameExport.OrganizeByStockNumber);
        Assert.Equal(CsvDelimiterType.Pipe, restored.FrameExport.CsvDelimiter);
        Assert.Equal(ExportFileFormat.Csv, restored.FrameExport.DefaultBomFormat);
        Assert.Equal(ExcelExportFileNameType.PartNumber, restored.FrameExport.BomFileNameType);
        Assert.Equal(["Material", "PartNumber", "UDP_Workshop"], restored.FrameExport.AttributeColumnOrder);
        Assert.Equal(ProcessingMethod.Hierarchy, restored.SelectedProcessingMethod);
        Assert.False(restored.Hierarchy.MergeDuplicateFiles);
        Assert.Equal("Workshop", Assert.Single(restored.FrameExport.TemplatePresets).Name);
        Assert.Equal(0, restored.FrameExport.SelectedTemplatePresetIndex);
        Assert.Equal(FlatPatternTopSideMode.MostBendsUp, restored.DxfExport.TopSideMode);
        Assert.Equal(AppTheme.Ocean, restored.Interface.SelectedTheme);
        Assert.Equal("{Angle} / {Radius}", restored.BendAnnotations.Template);
        Assert.Equal("Tahoma", restored.BendAnnotations.FontFamily);
        Assert.Equal("4.5", restored.BendAnnotations.TextHeight);
        Assert.True(restored.BendAnnotations.ConvertToCurves);
    }
}
