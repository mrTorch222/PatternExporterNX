using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Features.Frame.Models;
using FlatPatternExporter.Features.Frame.Services;
using FlatPatternExporter.Services;
using Inventor;
using IOPath = System.IO.Path;
using PropertyManager = FlatPatternExporter.Core.PropertyManager;

namespace FlatPatternExporter.Features.Frame.Core;

public sealed class FrameMemberScanner
{
    public const string FrameGeneratorInterestId = "{AC211AE0-A7A5-4589-916D-81C529DA6D17}";

    public FrameScanResult Scan(
        AssemblyDocument assemblyDocument,
        TubeRecognitionMode recognitionMode = TubeRecognitionMode.FrameGenerator,
        IReadOnlyCollection<string>? requestedAttributes = null,
        CancellationToken cancellationToken = default)
    {
        var members = new Dictionary<string, FrameMemberData>(StringComparer.OrdinalIgnoreCase);
        var documents = new Dictionary<string, PartDocument>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        var attributes = requestedAttributes?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        ScanOccurrences(assemblyDocument.ComponentDefinition.Occurrences, members, documents, errors, recognitionMode, attributes, cancellationToken);
        var assemblyDocumentObject = (Document)assemblyDocument;
        return new FrameScanResult(
            members.Values.OrderBy(member => member.StockNumber).ThenBy(member => member.LengthMm).ToList(),
            documents,
            errors,
            assemblyDocument.FullFileName ?? "",
            assemblyDocument.DisplayName ?? "",
            new PropertyManager(assemblyDocumentObject).GetMappedProperty("PartNumber"));
    }

    private static void ScanOccurrences(
        IEnumerable occurrences,
        IDictionary<string, FrameMemberData> members,
        IDictionary<string, PartDocument> documents,
        ICollection<string> errors,
        TubeRecognitionMode recognitionMode,
        IReadOnlySet<string> requestedAttributes,
        CancellationToken cancellationToken)
    {
        foreach (ComponentOccurrence occurrence in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (occurrence.Suppressed) continue;

                var definitionDocument = occurrence.Definition.Document;
                if (definitionDocument is PartDocument partDocument)
                {
                    TryAddMember(occurrence, partDocument, members, documents, errors, recognitionMode, requestedAttributes);
                }
                else if (definitionDocument is AssemblyDocument)
                {
                    ScanOccurrences(occurrence.SubOccurrences, members, documents, errors, recognitionMode, requestedAttributes, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var occurrenceName = TryGetOccurrenceName(occurrence);
                errors.Add($"{occurrenceName}: {ex.Message}");
                Debug.WriteLine($"Frame member scan failed for '{occurrenceName}': {ex}");
            }
        }
    }

    private static void TryAddMember(
        ComponentOccurrence occurrence,
        PartDocument partDocument,
        IDictionary<string, FrameMemberData> members,
        IDictionary<string, PartDocument> documents,
        ICollection<string> errors,
        TubeRecognitionMode recognitionMode,
        IReadOnlySet<string> requestedAttributes)
    {
        var document = (Document)partDocument;
        var propertyManager = new PropertyManager(document);
        double? lengthOverride = null;

        if (recognitionMode != TubeRecognitionMode.TubeJointProperties)
        {
            if (!IsFrameMember(document)) return;
        }
        else
        {
            var recognition = TubeJointPropertyRecognizer.Recognize(
                propertyName => propertyManager.GetMappedProperty($"UDP_{propertyName}"));
            if (!recognition.IsCandidate) return;
            if (!recognition.IsRecognized || recognition.Metadata is null)
            {
                errors.Add($"{TryGetOccurrenceName(occurrence)}: {recognition.Error}");
                return;
            }
            if (!IsSingleSolid(partDocument))
            {
                errors.Add($"{TryGetOccurrenceName(occurrence)}: {LocalizationManager.Instance.GetString("Tube_ErrorSingleSolidRequired")}");
                return;
            }

            lengthOverride = recognition.Metadata.LengthMm;
        }

        AddMember(occurrence, partDocument, propertyManager, lengthOverride, members, documents, requestedAttributes);
    }

    private static void AddMember(
        ComponentOccurrence occurrence,
        PartDocument partDocument,
        PropertyManager propertyManager,
        double? lengthOverride,
        IDictionary<string, FrameMemberData> members,
        IDictionary<string, PartDocument> documents,
        IReadOnlySet<string> requestedAttributes)
    {
        var document = (Document)partDocument;
        var fullFileName = partDocument.FullFileName ?? "";
        var fileName = string.IsNullOrWhiteSpace(fullFileName) ? partDocument.DisplayName : IOPath.GetFileName(fullFileName);
        var modelState = GetModelState(occurrence, propertyManager);
        var documentKey = FrameMemberIdentity.Create(fullFileName, partDocument.DisplayName, modelState);

        if (members.TryGetValue(documentKey, out var existingMember))
        {
            existingMember.Quantity++;
            return;
        }

        var member = new FrameMemberData
        {
            DocumentKey = documentKey,
            FileName = fileName,
            FullFileName = fullFileName,
            ModelState = modelState,
            PartNumber = propertyManager.GetMappedProperty("PartNumber"),
            StockNumber = propertyManager.GetMappedProperty("StockNumber"),
            Material = propertyManager.GetMappedProperty("Material"),
            Description = propertyManager.GetMappedProperty("Description"),
            LengthMm = lengthOverride ?? GetLengthMm(partDocument),
            Quantity = 1
        };

        foreach (var internalName in requestedAttributes)
        {
            var metadata = PropertyMetadataRegistry.GetPropertyByInternalName(internalName);
            if (metadata?.Type is not (PropertyMetadataRegistry.PropertyType.IProperty or PropertyMetadataRegistry.PropertyType.UserDefined))
                continue;
            var value = propertyManager.GetMappedProperty(internalName);
            member.SetAttributeValue(internalName, value);
            if (metadata.Type == PropertyMetadataRegistry.PropertyType.UserDefined && metadata.InventorPropertyName is { Length: > 0 } propertyName)
                member.UserDefinedProperties[propertyName] = value;
        }

        members.Add(documentKey, member);
        documents.Add(documentKey, partDocument);
    }

    private static string GetModelState(ComponentOccurrence occurrence, PropertyManager propertyManager)
    {
        try
        {
            return occurrence.ActiveModelState ?? "";
        }
        catch (Exception)
        {
            return propertyManager.GetModelState();
        }
    }

    private static bool IsFrameMember(Document document)
    {
        try
        {
            return document.DocumentInterests.HasInterest(FrameGeneratorInterestId);
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool IsSingleSolid(PartDocument document)
    {
        var definition = document.ComponentDefinition;
        return definition.SurfaceBodies.Count == 1 && !definition.HasMultipleSolidBodies && definition.SurfaceBodies[1].IsSolid;
    }

    private static double? GetLengthMm(PartDocument document)
    {
        try
        {
            var parameter = document.ComponentDefinition.Parameters["G_L"];
            var databaseCentimeters = Convert.ToDouble(parameter.Value, System.Globalization.CultureInfo.InvariantCulture);
            return FrameLengthConverter.FromInventorDatabaseCentimeters(databaseCentimeters);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string TryGetOccurrenceName(ComponentOccurrence occurrence)
    {
        try
        {
            return occurrence.Name;
        }
        catch
        {
            return LocalizationManager.Instance.GetString("Frame_UnknownMember");
        }
    }
}

public sealed record FrameScanResult(
    IReadOnlyList<FrameMemberData> Members,
    IReadOnlyDictionary<string, PartDocument> Documents,
    IReadOnlyList<string> Errors,
    string AssemblyFullFileName,
    string AssemblyDisplayName,
    string AssemblyPartNumber);
