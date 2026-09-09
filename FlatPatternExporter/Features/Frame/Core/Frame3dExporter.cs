using System.IO;
using FlatPatternExporter.Core;
using FlatPatternExporter.Enums;
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

        var errors = new List<string>();
        var exportedCount = 0;
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            member.ProcessingStatus = ProcessingStatus.Pending;
            member.OutputFile = "";

            if (!documents.TryGetValue(member.DocumentKey, out var document))
            {
                member.ProcessingStatus = ProcessingStatus.Skipped;
                errors.Add($"{member.FileName}: {LocalizationManager.Instance.GetString("Frame_ErrorDocumentUnavailable")}");
                continue;
            }

            var outputFolder = ResolveOutputFolder(member, options);
            Directory.CreateDirectory(outputFolder);
            TryDelete(IOPath.Combine(outputFolder, "3D_EXPORT_LOG.txt"));
            var baseName = FrameFileNameService.Resolve(options.FileNameTemplate, member);
            var uniqueName = FrameFileNameService.MakeUnique(baseName, reservedNames);
            var extension = GetFileExtension(options.ExportFormat);
            var outputPath = IOPath.Combine(outputFolder, uniqueName + extension);
            var temporaryPath = IOPath.Combine(outputFolder, $".{uniqueName}.{Guid.NewGuid():N}.tmp{extension}");

            try
            {
                if (!document.Update2(false))
                    throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorModelUpdate"));
                ValidateGeometry(document);
                ExportDocument(application, translator, (Document)document, temporaryPath, options);
                ValidateOutput(temporaryPath);
                IOFile.Move(temporaryPath, outputPath, true);
                member.OutputFile = outputPath;
                member.ProcessingStatus = ProcessingStatus.Success;
                exportedCount++;
            }
            catch (Exception ex)
            {
                TryDelete(temporaryPath);
                member.ProcessingStatus = ProcessingStatus.Skipped;
                errors.Add($"{member.FileName}: {ex.Message}");
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

    private static void ValidateGeometry(PartDocument document)
    {
        var definition = document.ComponentDefinition;
        if (definition.SurfaceBodies.Count != 1 || definition.HasMultipleSolidBodies)
            throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorSingleSolidRequired"));

        var body = definition.SurfaceBodies[1];
        if (!body.IsSolid)
            throw new InvalidOperationException(LocalizationManager.Instance.GetString("Frame_ErrorOpenSurfaceBody"));

    }

    private static string GetTranslatorId(FrameExportFormat format) => format switch
    {
        FrameExportFormat.Iges => IgesTranslatorId,
        FrameExportFormat.Step => StepTranslatorId,
        FrameExportFormat.Sat => SatTranslatorId,
        FrameExportFormat.Stl => StlTranslatorId,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static string ResolveOutputFolder(FrameMemberData member, FrameExportOptions options)
    {
        var folder = options.UsePartFolder
            ? IOPath.GetDirectoryName(member.FullFileName)
            : options.OutputFolder;
        if (string.IsNullOrWhiteSpace(folder)) folder = options.OutputFolder;
        if (options.EnableSubfolder && !string.IsNullOrWhiteSpace(options.SubfolderName))
            folder = IOPath.Combine(folder, SanitizeFolderName(options.SubfolderName));
        if (options.OrganizeByMaterial && !string.IsNullOrWhiteSpace(member.Material))
            folder = IOPath.Combine(folder, SanitizeFolderName(member.Material));
        if (options.OrganizeByStockNumber && !string.IsNullOrWhiteSpace(member.StockNumber))
            folder = IOPath.Combine(folder, SanitizeFolderName(member.StockNumber));
        return folder;
    }

    private static string SanitizeFolderName(string value)
    {
        var invalid = IOPath.GetInvalidFileNameChars();
        var sanitized = new string(value.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
    }

    private static string GetFileExtension(FrameExportFormat format) => format switch
    {
        FrameExportFormat.Iges => ".igs",
        FrameExportFormat.Step => ".stp",
        FrameExportFormat.Sat => ".sat",
        FrameExportFormat.Stl => ".stl",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

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
    int SurfaceType,
    FrameExportFormat ExportFormat = FrameExportFormat.Iges,
    bool UsePartFolder = false,
    bool EnableSubfolder = false,
    string SubfolderName = "",
    bool OrganizeByMaterial = false,
    bool OrganizeByStockNumber = false);

public sealed record FrameExportResult(int ExportedCount, IReadOnlyList<string> Errors);
