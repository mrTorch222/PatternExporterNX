using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlatPatternExporter.Features.Frame.Models;
using FlatPatternExporter.Services;
using Inventor;
using IOPath = System.IO.Path;
using PropertyManager = FlatPatternExporter.Core.PropertyManager;

namespace FlatPatternExporter.Features.Frame.Core;

public sealed class FrameMemberScanner
{
    public const string FrameGeneratorInterestId = "{AC211AE0-A7A5-4589-916D-81C529DA6D17}";

    public FrameScanResult Scan(AssemblyDocument assemblyDocument, CancellationToken cancellationToken = default)
    {
        var members = new Dictionary<string, FrameMemberData>(StringComparer.OrdinalIgnoreCase);
        var documents = new Dictionary<string, PartDocument>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        ScanOccurrences(assemblyDocument.ComponentDefinition.Occurrences, members, documents, errors, cancellationToken);
        return new FrameScanResult(members.Values.OrderBy(member => member.StockNumber).ThenBy(member => member.LengthMm).ToList(), documents, errors);
    }

    private static void ScanOccurrences(
        IEnumerable occurrences,
        IDictionary<string, FrameMemberData> members,
        IDictionary<string, PartDocument> documents,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        foreach (ComponentOccurrence occurrence in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (occurrence.Suppressed) continue;

                var definitionDocument = occurrence.Definition.Document;
                if (definitionDocument is PartDocument partDocument && IsFrameMember((Document)partDocument))
                {
                    AddMember(partDocument, members, documents);
                }
                else if (definitionDocument is AssemblyDocument)
                {
                    ScanOccurrences(occurrence.SubOccurrences, members, documents, errors, cancellationToken);
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

    private static void AddMember(
        PartDocument partDocument,
        IDictionary<string, FrameMemberData> members,
        IDictionary<string, PartDocument> documents)
    {
        var document = (Document)partDocument;
        var fullFileName = partDocument.FullFileName ?? "";
        var fileName = string.IsNullOrWhiteSpace(fullFileName) ? partDocument.DisplayName : IOPath.GetFileName(fullFileName);
        var documentKey = string.IsNullOrWhiteSpace(fullFileName) ? $"display:{partDocument.DisplayName}" : fullFileName;

        if (members.TryGetValue(documentKey, out var existingMember))
        {
            existingMember.Quantity++;
            return;
        }

        var propertyManager = new PropertyManager(document);
        var member = new FrameMemberData
        {
            DocumentKey = documentKey,
            FileName = fileName,
            FullFileName = fullFileName,
            PartNumber = propertyManager.GetMappedProperty("PartNumber"),
            StockNumber = propertyManager.GetMappedProperty("StockNumber"),
            Material = propertyManager.GetMappedProperty("Material"),
            Description = propertyManager.GetMappedProperty("Description"),
            LengthMm = GetLengthMm(partDocument),
            Quantity = 1
        };

        foreach (var property in PropertyMetadataRegistry.Properties.Values
                     .Where(property => property.Type == PropertyMetadataRegistry.PropertyType.IProperty))
        {
            member.SetAttributeValue(property.InternalName, propertyManager.GetMappedProperty(property.InternalName));
        }

        foreach (var property in PropertyMetadataRegistry.UserDefinedProperties)
        {
            var propertyName = property.InventorPropertyName;
            if (string.IsNullOrWhiteSpace(propertyName)) continue;
            var value = propertyManager.GetMappedProperty(property.InternalName);
            member.UserDefinedProperties[propertyName] = value;
            member.SetAttributeValue(property.InternalName, value);
        }

        members.Add(documentKey, member);
        documents.Add(documentKey, partDocument);
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

    private static double? GetLengthMm(PartDocument document)
    {
        try
        {
            var parameter = document.ComponentDefinition.Parameters["G_L"];
            return Convert.ToDouble(parameter.Value, System.Globalization.CultureInfo.InvariantCulture) * 10.0;
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
    IReadOnlyList<string> Errors);
