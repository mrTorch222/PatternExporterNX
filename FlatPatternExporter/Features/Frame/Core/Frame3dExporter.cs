using System.IO;
using FlatPatternExporter.Core;
using FlatPatternExporter.Enums;
using System.Globalization;
using System.Text;
using FlatPatternExporter.Features.Frame.Models;
using FlatPatternExporter.Features.Frame.Services;
using FlatPatternExporter.Services;
using Inventor;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace FlatPatternExporter.Features.Frame.Core;

public sealed class Frame3dExporter(InventorManager inventorManager)
{
    private const string IgesTranslatorId = "{90AF7F44-0C01-11D5-8E83-0010B541CD80}";
    private const string StepTranslatorId = "{90AF7F40-0C01-11D5-8E83-0010B541CD80}";
    private const string SatTranslatorId = "{89162634-02B6-11D5-8E80-0010B541CD80}";
    private const string StlTranslatorId = "{533E9A98-FC3B-11D4-8E7E-0010B541CD80}";
    private const double ExportFitToleranceCm = 0.001;

    public FrameExportResult Export(
        IEnumerable<FrameMemberData> members,
        IReadOnlyDictionary<string, PartDocument> documents,
        FrameExportOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!inventorManager.EnsureInventorConnection())
            throw new InvalidOperationException(LocalizationManager.Instance.GetString("Error_InventorConnectionFailed"));

        var application = inventorManager.Application ?? throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorInventorUnavailable"));
        var translator = (TranslatorAddIn)application.ApplicationAddIns.ItemById[GetTranslatorId(options.ExportFormat)];
        if (!translator.Activated) translator.Activate();

        Directory.CreateDirectory(options.OutputFolder);
        var errors = new List<string>();
        var itemResults = new List<FrameExportItemResult>();
        var exportedCount = 0;
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var log = new List<string>
        {
            "Frame Generator 3D Exporter",
            $"Time={DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Format={options.ExportFormat}",
            $"IGES options: GeometryType={options.GeometryType}, SolidFaceType={options.SolidFaceType}, SurfaceType={options.SurfaceType}, IncludeSketches=False, ToleranceCm={ExportFitToleranceCm.ToString(CultureInfo.InvariantCulture)}"
        };

        try
        {
            foreach (var member in members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!documents.TryGetValue(member.DocumentKey, out var document))
                {
                    var error = $"{member.FileName}: {LocalizationManager.Instance.GetString("Frame_ErrorDocumentUnavailable")}";
                    errors.Add(error);
                    itemResults.Add(new FrameExportItemResult(member.DocumentKey, "", ProcessingStatus.Failed, error));
                    log.Add($"SKIPPED | {member.FileName} | {errors[^1]}");
                    continue;
                }

                var baseName = FrameFileNameService.Resolve(options.FileNameTemplate, member);
                var uniqueName = FrameFileNameService.MakeUnique(baseName, reservedNames);
                var extension = GetFileExtension(options.ExportFormat);
                var outputPath = IOPath.Combine(options.OutputFolder, uniqueName + extension);
                var temporaryPath = IOPath.Combine(options.OutputFolder, $".{uniqueName}.{Guid.NewGuid():N}.tmp{extension}");

                try
                {
                    if (!document.Update2(false))
                        throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorModelUpdate"));
                    var geometrySummary = ValidateAndDescribeGeometry(document);
                    ExportDocument(application, translator, (Document)document, temporaryPath, options);
                    FrameExportValidator.Validate(temporaryPath, options.ExportFormat);
                    var outputSize = new FileInfo(temporaryPath).Length;
                    IOFile.Move(temporaryPath, outputPath, true);
                    itemResults.Add(new FrameExportItemResult(member.DocumentKey, outputPath, ProcessingStatus.Success, null));
                    exportedCount++;
                    log.Add($"OK | {member.FileName} | {geometrySummary} | Bytes={outputSize} | {outputPath}");
                }
                catch (Exception ex)
                {
                    TryDelete(temporaryPath);
                    var error = $"{member.FileName}: {ex.Message}";
                    errors.Add(error);
                    itemResults.Add(new FrameExportItemResult(member.DocumentKey, "", ProcessingStatus.Failed, error));
                    log.Add($"EXPORT ERROR | {member.FileName} | {ex.Message.ReplaceLineEndings(" ")}");
                }
            }
        }
        finally
        {
            try
            {
                IOFile.WriteAllLines(IOPath.Combine(options.OutputFolder, "3D_EXPORT_LOG.txt"), log, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                errors.Add(LocalizationManager.Instance.GetString("Frame_ErrorLogWrite", ex.Message));
            }
        }

        return new FrameExportResult(exportedCount, errors, itemResults);
    }

    private static void ExportDocument(
        Inventor.Application application,
        TranslatorAddIn translator,
        Document document,
        string outputPath,
        FrameExportOptions options)
    {
        var context = application.TransientObjects.CreateTranslationContext();
        context.Type = IOMechanismEnum.kFileBrowseIOMechanism;
        var translatorOptions = application.TransientObjects.CreateNameValueMap();

        if (!translator.HasSaveCopyAsOptions[document, context, translatorOptions])
            throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorTranslatorOptions"));

        if (options.ExportFormat == FrameExportFormat.Iges)
        {
            translatorOptions.Value["GeometryType"] = options.GeometryType;
            translatorOptions.Value["SolidFaceType"] = options.SolidFaceType;
            translatorOptions.Value["SurfaceType"] = options.SurfaceType;
            translatorOptions.Value["IncludeSketches"] = false;
            translatorOptions.Value["export_fit_tolerance"] = ExportFitToleranceCm;
        }
        else if (options.ExportFormat == FrameExportFormat.Stl)
        {
            translatorOptions.Value["Resolution"] = 1;
            translatorOptions.Value["SurfaceDeviation"] = 0.01;
            translatorOptions.Value["NormalDeviation"] = 0.5;
            translatorOptions.Value["MaxEdgeLength"] = 100.0;
            translatorOptions.Value["AspectRatio"] = 21.5;
            translatorOptions.Value["ExportUnits"] = 5;
        }

        var dataMedium = application.TransientObjects.CreateDataMedium();
        dataMedium.FileName = outputPath;
        translator.SaveCopyAs(document, context, translatorOptions, dataMedium);
    }

    private static string ValidateAndDescribeGeometry(PartDocument document)
    {
        var definition = document.ComponentDefinition;
        if (definition.SurfaceBodies.Count != 1 || definition.HasMultipleSolidBodies)
            throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorSingleSolidRequired"));

        var body = definition.SurfaceBodies[1];
        if (!body.IsSolid)
            throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorOpenSurfaceBody"));

        double? volume = null;
        try { volume = body.Volume[0.01]; }
        catch { }

        var unhealthy = new List<string>();
        try
        {
            foreach (PartFeature feature in definition.Features)
                if (feature.HealthStatus != HealthStatusEnum.kUpToDateHealth)
                    unhealthy.Add($"{feature.Name}={feature.HealthStatus}");
        }
        catch { }

        return $"Solids=1, Faces={body.Faces.Count}, Edges={body.Edges.Count}, VolumeCm3={volume?.ToString("0.######", CultureInfo.InvariantCulture) ?? "unknown"}, NonUpToDateFeatures={(unhealthy.Count == 0 ? "none" : string.Join(",", unhealthy))}";
    }

    private static string GetTranslatorId(FrameExportFormat format) => format switch
    {
        FrameExportFormat.Iges => IgesTranslatorId,
        FrameExportFormat.Step => StepTranslatorId,
        FrameExportFormat.Sat => SatTranslatorId,
        FrameExportFormat.Stl => StlTranslatorId,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static string GetFileExtension(FrameExportFormat format) => format switch
    {
        FrameExportFormat.Iges => ".igs",
        FrameExportFormat.Step => ".stp",
        FrameExportFormat.Sat => ".sat",
        FrameExportFormat.Stl => ".stl",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static void TryDelete(string filePath)
    {
        try
        {
            if (IOFile.Exists(filePath)) IOFile.Delete(filePath);
        }
        catch
        {
        }
    }
}

public sealed record FrameExportOptions(
    string OutputFolder,
    string FileNameTemplate,
    int GeometryType,
    int SolidFaceType,
    int SurfaceType,
    FrameExportFormat ExportFormat = FrameExportFormat.Iges);

public sealed record FrameExportItemResult(string DocumentKey, string OutputFile, ProcessingStatus Status, string? Error);

public sealed record FrameExportResult(
    int ExportedCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<FrameExportItemResult> Items);
