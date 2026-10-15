using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using FlatPatternExporter.Models;
using FlatPatternExporter.Services;
using FlatPatternExporter.Utilities;
using Inventor;

namespace FlatPatternExporter.Core;

public class PartDataReader
{
    private readonly InventorManager _inventorManager;
    private readonly DocumentScanner _documentScanner;
    private readonly ThumbnailGenerator _thumbnailGenerator;
    private readonly Dispatcher _dispatcher;

    public PartDataReader(InventorManager inventorManager, DocumentScanner documentScanner, ThumbnailGenerator thumbnailGenerator, Dispatcher dispatcher)
    {
        _inventorManager = inventorManager;
        _documentScanner = documentScanner;
        _thumbnailGenerator = thumbnailGenerator;
        _dispatcher = dispatcher;
    }

    public async Task<PartData> GetPartDataAsync(
        string partNumber,
        int quantity,
        int itemNumber,
        bool loadThumbnail = true,
        IReadOnlySet<string>? displayedColumns = null)
    {
        var partDoc = _documentScanner.DocumentCache.GetCachedPartDocument(partNumber) ?? _inventorManager.OpenPartDocument(partNumber);
        if (partDoc == null) return null!;

        return await GetPartDataAsync(partDoc, quantity, itemNumber, loadThumbnail, displayedColumns);
    }

    public async Task<PartData> GetPartDataAsync(
        PartDocument partDoc,
        int quantity,
        int itemNumber,
        bool loadThumbnail = true,
        IReadOnlySet<string>? displayedColumns = null)
    {
        var partData = new PartData
        {
            Item = itemNumber,
            OriginalQuantity = quantity
        };

        var mgr = new PropertyManager((Document)partDoc);

        ReadAllPropertiesFromPart(partDoc, partData, mgr, displayedColumns);

        var userValues = mgr.GetMappedProperties(
            PropertyMetadataRegistry.UserDefinedProperties.Select(property => property.InternalName));
        foreach (var userDefinedProperty in PropertyMetadataRegistry.UserDefinedProperties)
        {
            var propertyName = userDefinedProperty.InventorPropertyName ?? "";
            partData.UserDefinedProperties[propertyName] = userValues[userDefinedProperty.InternalName];
        }

        if (loadThumbnail)
        {
            partData.Preview = await _thumbnailGenerator.GetThumbnailAsync(partDoc, _dispatcher);
        }

        partData.SetQuantityInternal(quantity);

        return partData;
    }

    private static void ReadAllPropertiesFromPart(
        PartDocument partDoc,
        PartData partData,
        PropertyManager mgr,
        IReadOnlySet<string>? displayedColumns)
    {
        partData.FileName = System.IO.Path.GetFileNameWithoutExtension(partDoc.FullFileName);
        partData.FullFileName = partDoc.FullFileName;
        partData.ModelState = mgr.GetModelState();
        partData.HasFlatPattern = mgr.HasFlatPattern();
        partData.Thickness = mgr.GetThickness();
        partData.DocumentLengthUnit = partDoc.UnitsOfMeasure.LengthUnits switch
        {
            UnitsTypeEnum.kMillimeterLengthUnits => DocumentLengthUnit.Millimeter,
            UnitsTypeEnum.kMeterLengthUnits => DocumentLengthUnit.Meter,
            UnitsTypeEnum.kCentimeterLengthUnits => DocumentLengthUnit.Centimeter,
            UnitsTypeEnum.kInchLengthUnits => DocumentLengthUnit.Inch,
            UnitsTypeEnum.kFootLengthUnits => DocumentLengthUnit.Foot,
            _ => DocumentLengthUnit.Unknown
        };
        if (partDoc.ComponentDefinition is SheetMetalComponentDefinition sheetMetalDefinition)
        {
            var bendMetrics = SheetMetalBendAnalyzer.Analyze(sheetMetalDefinition);
            partData.BendCount = bendMetrics.Count;
            partData.LongestBendLengthMm = bendMetrics.LongestLengthMm;
            partData.BendsUpCount = bendMetrics.UpCount;
            partData.BendsDownCount = bendMetrics.DownCount;
        }

        if (displayedColumns is not null)
            SetExpressionStatesForDisplayedProperties(partData, mgr, displayedColumns);

        var values = mgr.GetMappedProperties(PropertyMetadataRegistry.Properties.Values
            .Where(property => property.Type == PropertyMetadataRegistry.PropertyType.IProperty)
            .Select(property => property.InternalName));
        foreach (var (internalName, value) in values)
        {
            var propInfo = typeof(PartData).GetProperty(internalName);

            if (propInfo != null && !string.IsNullOrEmpty(value))
            {
                propInfo.SetValue(partData, value);
            }
        }
    }

    private static void SetExpressionStatesForDisplayedProperties(
        PartData partData,
        PropertyManager mgr,
        IReadOnlySet<string> displayedColumns)
    {
        partData.BeginExpressionBatch();
        try
        {
            foreach (var property in PropertyMetadataRegistry.GetEditableProperties().Where(displayedColumns.Contains))
            {
                var isExpression = mgr.IsMappedPropertyExpression(property);
                partData.SetPropertyExpressionState(property, isExpression);
            }
            foreach (var userProperty in PropertyMetadataRegistry.UserDefinedProperties
                         .Where(property => displayedColumns.Contains(property.InternalName)))
            {
                var isExpression = mgr.IsMappedPropertyExpression(userProperty.InternalName);
                var propertyName = userProperty.InventorPropertyName ?? "";
                partData.SetPropertyExpressionState($"UserDefinedProperties[{propertyName}]", isExpression);
            }
        }
        finally
        {
            partData.EndExpressionBatch();
        }
    }

    public void FillPropertyData(ObservableCollection<PartData> partsData, string propertyName)
    {
        if (partsData.Count == 0)
            return;

        var standardProperty = PropertyMetadataRegistry.Properties.GetValueOrDefault(propertyName);

        if (standardProperty is { IsEditable: false })
        {
            return;
        }

        // For user-defined properties, extract the actual property name from InternalName
        var actualPropertyName = PropertyMetadataRegistry.IsUserDefinedProperty(propertyName)
            ? PropertyMetadataRegistry.GetInventorNameFromUserDefinedInternalName(propertyName)
            : propertyName;

        foreach (var partData in partsData)
        {
            var partDoc = _documentScanner.DocumentCache.GetCachedPartDocument(partData.PartNumber) ?? _inventorManager.OpenPartDocument(partData.PartNumber);
            if (partDoc != null)
            {
                var mgr = new PropertyManager((Document)partDoc);
                if (standardProperty is not null)
                {
                    partData.SetPropertyExpressionState(propertyName, mgr.IsMappedPropertyExpression(propertyName));
                    continue;
                }

                var value = mgr.GetMappedProperty(propertyName) ?? "";
                partData.AddUserDefinedProperty(actualPropertyName, value);

                var isExpression = mgr.IsMappedPropertyExpression(propertyName);
                partData.SetPropertyExpressionState($"UserDefinedProperties[{actualPropertyName}]", isExpression);
            }
        }
    }

    public void UpdateQuantitiesWithMultiplier(ObservableCollection<PartData> partsData, int multiplier)
    {
        foreach (var partData in partsData)
        {
            partData.IsOverridden = false;
            partData.SetQuantityInternal(partData.OriginalQuantity * multiplier);
            partData.IsMultiplied = multiplier > 1;
        }
    }

    public DocumentInfo GetDocumentInfo(Document? doc)
    {
        if (doc == null)
        {
            return new DocumentInfo
            {
                DocumentType = "",
                PartNumber = "",
                Description = "",
                ModelState = "",
                IsPrimaryModelState = true
            };
        }

        var mgr = new PropertyManager(doc);
        return new DocumentInfo
        {
            DocumentType = doc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject ? LocalizationManager.Instance.GetString("DocumentType_Assembly") : LocalizationManager.Instance.GetString("DocumentType_Part"),
            PartNumber = mgr.GetMappedProperty("PartNumber"),
            Description = mgr.GetMappedProperty("Description"),
            ModelState = mgr.GetModelState(),
            IsPrimaryModelState = mgr.IsPrimaryModelState()
        };
    }
}

public class DocumentInfo
{
    public string DocumentType { get; set; } = "";
    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string ModelState { get; set; } = "";
    public bool IsPrimaryModelState { get; set; } = true;
}
