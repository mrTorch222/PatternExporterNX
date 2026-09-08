using System.IO;
using FlatPatternExporter.Enums;
using System.Globalization;
using System.Text;
using FlatPatternExporter.Models;
using FlatPatternExporter.Services;
using Inventor;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace FlatPatternExporter.Core;

public sealed class FrameIgesExporter(InventorManager inventorManager)
{
    private const string IgesTranslatorId = "{90AF7F44-0C01-11D5-8E83-0010B541CD80}";
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
        var translator = (TranslatorAddIn)application.ApplicationAddIns.ItemById[IgesTranslatorId];
        if (!translator.Activated) translator.Activate();

        Directory.CreateDirectory(options.OutputFolder);
        var errors = new List<string>();
        var exportedCount = 0;
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var log = new List<string>
        {
            "Frame Generator IGES Exporter",
            $"Time={DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Options: GeometryType={options.GeometryType}, SolidFaceType={options.SolidFaceType}, SurfaceType={options.SurfaceType}, IncludeSketches=False, ToleranceCm={ExportFitToleranceCm.ToString(CultureInfo.InvariantCulture)}"
        };

        try
        {
            foreach (var member in members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                member.ProcessingStatus = ProcessingStatus.Pending;
                member.OutputFile = "";

                if (!documents.TryGetValue(member.DocumentKey, out var document))
                {
                    member.ProcessingStatus = ProcessingStatus.Skipped;
                    errors.Add($"{member.FileName}: {LocalizationManager.Instance.GetString("Frame_ErrorDocumentUnavailable")}");
                    log.Add($"SKIPPED | {member.FileName} | {errors[^1]}");
                    continue;
                }

                var baseName = FrameFileNameService.Resolve(options.FileNameTemplate, member);
                var uniqueName = FrameFileNameService.MakeUnique(baseName, reservedNames);
                var outputPath = IOPath.Combine(options.OutputFolder, uniqueName + ".igs");
                var temporaryPath = IOPath.Combine(options.OutputFolder, $".{uniqueName}.{Guid.NewGuid():N}.tmp.igs");

                try
                {
                    if (!document.Update2(false))
                        throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorModelUpdate"));
                    var geometrySummary = ValidateAndDescribeGeometry(document);
                    ExportDocument(application, translator, (Document)document, temporaryPath, options);
                    ValidateOutput(temporaryPath);
                    var outputSize = new FileInfo(temporaryPath).Length;
                    IOFile.Move(temporaryPath, outputPath, true);
                    member.OutputFile = outputPath;
                    member.ProcessingStatus = ProcessingStatus.Success;
                    exportedCount++;
                    log.Add($"OK | {member.FileName} | {geometrySummary} | Bytes={outputSize} | {outputPath}");
                }
                catch (Exception ex)
                {
                    TryDelete(temporaryPath);
                    member.ProcessingStatus = ProcessingStatus.Skipped;
                    errors.Add($"{member.FileName}: {ex.Message}");
                    log.Add($"EXPORT ERROR | {member.FileName} | {ex.Message.ReplaceLineEndings(" ")}");
                }
            }
        }
        finally
        {
            try
            {
                IOFile.WriteAllLines(IOPath.Combine(options.OutputFolder, "IGES_EXPORT_LOG.txt"), log, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                errors.Add(LocalizationManager.Instance.GetString("Frame_ErrorLogWrite", ex.Message));
            }
        }

        return new FrameExportResult(exportedCount, errors);
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

        translatorOptions.Value["GeometryType"] = options.GeometryType;
        translatorOptions.Value["SolidFaceType"] = options.SolidFaceType;
        translatorOptions.Value["SurfaceType"] = options.SurfaceType;
        translatorOptions.Value["IncludeSketches"] = false;
        translatorOptions.Value["export_fit_tolerance"] = ExportFitToleranceCm;

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

    private static void ValidateOutput(string filePath)
    {
        var file = new FileInfo(filePath);
        if (!file.Exists || file.Length == 0)
            throw new IOException(LocalizationManager.Instance.GetString("Frame_ErrorInvalidIges"));
    }

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
    int SurfaceType);

public sealed record FrameExportResult(int ExportedCount, IReadOnlyList<string> Errors);
