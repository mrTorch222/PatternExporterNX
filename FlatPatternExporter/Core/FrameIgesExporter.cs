using System.IO;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Models;
using FlatPatternExporter.Services;
using Inventor;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace FlatPatternExporter.Core;

public sealed class FrameIgesExporter(InventorManager inventorManager)
{
    private const string IgesTranslatorId = "{90AF7F44-0C01-11D5-8E83-0010B541CD80}";

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

            var baseName = FrameFileNameService.Resolve(options.FileNameTemplate, member);
            var uniqueName = FrameFileNameService.MakeUnique(baseName, reservedNames);
            var outputPath = IOPath.Combine(options.OutputFolder, uniqueName + ".igs");
            var temporaryPath = IOPath.Combine(options.OutputFolder, $".{uniqueName}.{Guid.NewGuid():N}.tmp.igs");

            try
            {
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

        if (translator.HasSaveCopyAsOptions[document, context, translatorOptions])
        {
            translatorOptions.Value["GeometryType"] = options.GeometryType;
            translatorOptions.Value["SolidFaceType"] = options.SolidFaceType;
            translatorOptions.Value["SurfaceType"] = options.SurfaceType;
        }

        var dataMedium = application.TransientObjects.CreateDataMedium();
        dataMedium.FileName = outputPath;
        translator.SaveCopyAs(document, context, translatorOptions, dataMedium);
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
