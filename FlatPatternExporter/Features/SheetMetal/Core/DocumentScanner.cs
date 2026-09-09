using System.Diagnostics;
using System.Runtime.InteropServices;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Models;
using FlatPatternExporter.Services;
using Inventor;
using PropertyManager = FlatPatternExporter.Core.PropertyManager;

namespace FlatPatternExporter.Core;

public class DocumentScanner
{
    private readonly InventorManager _inventorManager;
    private readonly DocumentCache _documentCache;
    private readonly ConflictAnalyzer _conflictAnalyzer;
    private bool _hasMissingReferences;

    public DocumentScanner(InventorManager inventorManager)
    {
        _inventorManager = inventorManager;
        _documentCache = new DocumentCache();
        _conflictAnalyzer = new ConflictAnalyzer();
    }

    public DocumentCache DocumentCache => _documentCache;
    public ConflictAnalyzer ConflictAnalyzer => _conflictAnalyzer;
    public bool HasMissingReferences => _hasMissingReferences;

    public async Task<ScanResult> ScanDocumentAsync(
        Document document,
        ProcessingMethod processingMethod,
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScanResult
        {
            ProcessingMethod = processingMethod
        };

        try
        {
            ClearCaches();
            _hasMissingReferences = false;

            var sheetMetalParts = new Dictionary<string, int>();

            if (document.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                var asmDoc = (AssemblyDocument)document;

                if (processingMethod == ProcessingMethod.Traverse)
                    await Task.Run(() => ProcessComponentOccurrences(asmDoc.ComponentDefinition.Occurrences, sheetMetalParts, options, progress, cancellationToken), cancellationToken);
                else if (processingMethod == ProcessingMethod.BOM)
                    await Task.Run(() => ProcessBOM(asmDoc.ComponentDefinition.BOM, sheetMetalParts, options, progress, cancellationToken), cancellationToken);
                else if (processingMethod == ProcessingMethod.Hierarchy)
                {
                    var root = new AssemblyHierarchyNode
                    {
                        Id = "0",
                        Name = asmDoc.DisplayName,
                        FullFileName = asmDoc.FullFileName
                    };
                    result.HierarchyRoots.Add(root);
                    await Task.Run(() => ProcessHierarchyOccurrences(
                        asmDoc.ComponentDefinition.Occurrences,
                        root,
                        sheetMetalParts,
                        result.HierarchyContributions,
                        options,
                        progress,
                        cancellationToken), cancellationToken);
                }

                await _conflictAnalyzer.AnalyzeConflictsAsync();
                _conflictAnalyzer.FilterConflictingParts(sheetMetalParts);
                if (processingMethod == ProcessingMethod.Hierarchy)
                {
                    result.HierarchyContributions.RemoveAll(item =>
                        !sheetMetalParts.ContainsKey(item.PartNumber));
                }

                result.SheetMetalParts = sheetMetalParts;
                result.ProcessedCount = sheetMetalParts.Count;
            }
            else if (document.DocumentType == DocumentTypeEnum.kPartDocumentObject)
            {
                var partDoc = (PartDocument)document;

                if (!options.IncludeLibraryComponents && _inventorManager.IsLibraryComponent(partDoc.FullFileName))
                {
                    result.ProcessedCount = 0;
                    return result;
                }

                var mgr = new PropertyManager((Document)partDoc);
                var partNumber = mgr.GetMappedProperty("PartNumber");
                if (!string.IsNullOrEmpty(partNumber))
                {
                    _documentCache.AddDocumentToCache(partDoc, partNumber);

                    if (partDoc.SubType == PropertyManager.SheetMetalSubType)
                    {
                        sheetMetalParts.Add(partNumber, 1);
                        result.ProcessedCount = 1;
                        result.SheetMetalParts = sheetMetalParts;
                    }
                }
            }

            result.WasCancelled = cancellationToken.IsCancellationRequested;
            result.HasMissingReferences = _hasMissingReferences;
        }
        catch (OperationCanceledException)
        {
            result.WasCancelled = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedTime = stopwatch.Elapsed;
        }

        return result;
    }

    private void ProcessComponentOccurrences(
        ComponentOccurrences occurrences,
        Dictionary<string, int> sheetMetalParts,
        ScanOptions options,
        IProgress<ScanProgress>? scanProgress = null,
        CancellationToken cancellationToken = default)
    {
        var filteredOccurrences = new List<ComponentOccurrence>();
        foreach (ComponentOccurrence occ in occurrences)
        {
            try
            {
                if (occ.Suppressed) continue;

                if (occ.Definition is VirtualComponentDefinition) continue;

                var fullFileName = GetFullFileName(occ);
                if (!ShouldExcludeComponent(occ.BOMStructure, fullFileName, options))
                    filteredOccurrences.Add(occ);
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80004005))
            {
                _hasMissingReferences = true;
                Debug.WriteLine($"Detected component with missing reference: {ex.Message}");
            }
            catch
            {
            }
        }

        var totalOccurrences = filteredOccurrences.Count;
        var processedOccurrences = 0;

        foreach (ComponentOccurrence occ in filteredOccurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (occ.DefinitionDocumentType == DocumentTypeEnum.kPartDocumentObject)
                {
                    if (occ.Definition.Document is PartDocument partDoc)
                    {
                        var mgr = new PropertyManager((Document)partDoc);
                        var partNumber = mgr.GetMappedProperty("PartNumber");
                        if (!string.IsNullOrEmpty(partNumber))
                        {
                            _documentCache.AddDocumentToCache(partDoc, partNumber);

                            if (partDoc.SubType == PropertyManager.SheetMetalSubType)
                            {
                                var modelState = mgr.GetModelState();
                                _conflictAnalyzer.AddPartToTracker(partNumber, partDoc.FullFileName, modelState);

                                if (sheetMetalParts.TryGetValue(partNumber, out var quantity))
                                    sheetMetalParts[partNumber]++;
                                else
                                    sheetMetalParts.Add(partNumber, 1);
                            }
                        }
                    }
                }
                else if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    ProcessComponentOccurrences((ComponentOccurrences)occ.SubOccurrences, sheetMetalParts, options, cancellationToken: cancellationToken);
                }

                processedOccurrences++;
                scanProgress?.Report(new ScanProgress
                {
                    ProcessedItems = processedOccurrences,
                    TotalItems = totalOccurrences,
                    CurrentOperation = LocalizationManager.Instance.GetString("Status_ScanningComponents"),
                    CurrentItem = LocalizationManager.Instance.GetString("Status_ComponentProgress", processedOccurrences, totalOccurrences)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing component: {ex.Message}");

                processedOccurrences++;
                scanProgress?.Report(new ScanProgress
                {
                    ProcessedItems = processedOccurrences,
                    TotalItems = totalOccurrences,
                    CurrentOperation = LocalizationManager.Instance.GetString("Status_ScanningComponents"),
                    CurrentItem = LocalizationManager.Instance.GetString("Status_ComponentProgress", processedOccurrences, totalOccurrences)
                });
            }
        }
    }

    private void ProcessBOM(
        BOM bom,
        Dictionary<string, int> sheetMetalParts,
        ScanOptions options,
        IProgress<ScanProgress>? scanProgress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allBomRows = GetAllBOMRowsRecursively(bom, options);

            Debug.WriteLine($"[ProcessBOM] Total found {allBomRows.Count} BOM rows in entire structure");

            var processedRows = 0;
            var totalRows = allBomRows.Count;

            foreach (var (row, parentQuantity) in allBomRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    ProcessBOMRowSimple(row, sheetMetalParts, parentQuantity);

                    processedRows++;
                    scanProgress?.Report(new ScanProgress
                    {
                        ProcessedItems = processedRows,
                        TotalItems = totalRows,
                        CurrentOperation = LocalizationManager.Instance.GetString("Status_ScanningBOM"),
                        CurrentItem = LocalizationManager.Instance.GetString("Status_BomRowProgress", processedRows, totalRows)
                    });
                }
                catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80004005))
                {
                    _hasMissingReferences = true;
                    Debug.WriteLine($"Detected component with missing reference: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing BOM row: {ex.Message}");
                    _hasMissingReferences = true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"General error processing BOM: {ex.Message}");
            _hasMissingReferences = true;
        }
    }

    private void ProcessHierarchyOccurrences(
        ComponentOccurrences occurrences,
        AssemblyHierarchyNode parent,
        Dictionary<string, int> sheetMetalParts,
        List<PartOccurrenceContribution> contributions,
        ScanOptions options,
        IProgress<ScanProgress>? scanProgress,
        CancellationToken cancellationToken)
    {
        var occurrenceIndex = 0;
        foreach (ComponentOccurrence occurrence in occurrences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            occurrenceIndex++;

            try
            {
                if (occurrence.Suppressed || occurrence.Definition is VirtualComponentDefinition) continue;
                var fullFileName = GetFullFileName(occurrence);
                if (ShouldExcludeComponent(occurrence.BOMStructure, fullFileName, options)) continue;

                if (occurrence.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    var assembly = (AssemblyDocument)occurrence.Definition.Document;
                    var child = new AssemblyHierarchyNode
                    {
                        Id = $"{parent.Id}.{occurrenceIndex}",
                        Name = occurrence.Name,
                        FullFileName = assembly.FullFileName,
                        Parent = parent
                    };
                    parent.Children.Add(child);
                    ProcessHierarchyOccurrences(
                        (ComponentOccurrences)occurrence.SubOccurrences,
                        child,
                        sheetMetalParts,
                        contributions,
                        options,
                        scanProgress,
                        cancellationToken);
                    continue;
                }

                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kPartDocumentObject ||
                    occurrence.Definition.Document is not PartDocument partDoc)
                    continue;

                var manager = new PropertyManager((Document)partDoc);
                var partNumber = manager.GetMappedProperty("PartNumber");
                if (string.IsNullOrWhiteSpace(partNumber)) continue;
                _documentCache.AddDocumentToCache(partDoc, partNumber);
                if (partDoc.SubType != PropertyManager.SheetMetalSubType) continue;

                _conflictAnalyzer.AddPartToTracker(partNumber, partDoc.FullFileName, manager.GetModelState());
                sheetMetalParts[partNumber] = sheetMetalParts.GetValueOrDefault(partNumber) + 1;
                parent.DirectPartCount++;

                var existingIndex = contributions.FindIndex(item =>
                    item.AssemblyNodeId == parent.Id &&
                    string.Equals(item.PartNumber, partNumber, StringComparison.OrdinalIgnoreCase));
                if (existingIndex >= 0)
                    contributions[existingIndex] = contributions[existingIndex] with { BaseQuantity = contributions[existingIndex].BaseQuantity + 1 };
                else
                    contributions.Add(new PartOccurrenceContribution(partNumber, parent.Id, BuildStructurePath(parent), 1));
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80004005))
            {
                _hasMissingReferences = true;
                Debug.WriteLine($"Detected component with missing reference: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing hierarchy component: {ex.Message}");
            }

            scanProgress?.Report(new ScanProgress
            {
                ProcessedItems = occurrenceIndex,
                TotalItems = occurrences.Count,
                CurrentOperation = LocalizationManager.Instance.GetString("Status_ScanningComponents"),
                CurrentItem = LocalizationManager.Instance.GetString("Status_ComponentProgress", occurrenceIndex, occurrences.Count)
            });
        }
    }

    private static string BuildStructurePath(AssemblyHierarchyNode node)
    {
        var names = new Stack<string>();
        for (var current = node; current is not null; current = current.Parent)
            names.Push(current.Name);
        return string.Join(" / ", names);
    }

    private List<(BOMRow Row, int ParentQuantity)> GetAllBOMRowsRecursively(BOM bom, ScanOptions options, int parentQuantity = 1)
    {
        var allRows = new List<(BOMRow, int)>();

        try
        {
            var hideSuppressed = bom.HideSuppressedComponentsInBOM;

            BOMView? bomView = null;
            foreach (BOMView view in bom.BOMViews)
            {
                if (view.ViewType == BOMViewTypeEnum.kModelDataBOMViewType)
                {
                    bomView = view;
                    break;
                }
            }

            if (bomView == null) return allRows;

            try
            {
                var bomRows = bomView.BOMRows.Cast<BOMRow>().ToArray();

                foreach (BOMRow row in bomRows)
                {
                    if (!hideSuppressed && row.ItemQuantity <= 0)
                        continue;

                    var componentDefinition = row.ComponentDefinitions[1];

                    if (componentDefinition is VirtualComponentDefinition)
                    {
                        continue;
                    }

                    if (componentDefinition?.Document is Document document &&
                        ShouldExcludeComponent(row.BOMStructure, document.FullFileName, options))
                        continue;

                    allRows.Add((row, parentQuantity));

                    try
                    {
                        if (componentDefinition?.Document is AssemblyDocument asmDoc)
                        {
                            var totalQuantity = parentQuantity * row.ItemQuantity;
                            var subRows = GetAllBOMRowsRecursively(asmDoc.ComponentDefinition.BOM, options, totalQuantity);
                            allRows.AddRange(subRows);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80004005))
            {
                _hasMissingReferences = true;
                Debug.WriteLine("Error accessing BOMRows. Assembly may have missing references.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error recursively getting BOM: {ex.Message}");
        }

        return allRows;
    }

    private void ProcessBOMRowSimple(BOMRow row, Dictionary<string, int> sheetMetalParts, int parentQuantity = 1)
    {
        try
        {
            var componentDefinition = row.ComponentDefinitions[1];
            if (componentDefinition == null) return;

            if (componentDefinition.Document is not Document document) return;

            if (document.DocumentType == DocumentTypeEnum.kPartDocumentObject)
            {
                if (document is PartDocument partDoc)
                {
                    var mgr = new PropertyManager((Document)partDoc);
                    var partNumber = mgr.GetMappedProperty("PartNumber");
                    if (!string.IsNullOrEmpty(partNumber))
                    {
                        _documentCache.AddDocumentToCache(partDoc, partNumber);

                        if (partDoc.SubType == PropertyManager.SheetMetalSubType)
                        {
                            var modelState = mgr.GetModelState();
                            _conflictAnalyzer.AddPartToTracker(partNumber, partDoc.FullFileName, modelState);

                            var totalQuantity = row.ItemQuantity * parentQuantity;

                            if (sheetMetalParts.TryGetValue(partNumber, out var quantity))
                                sheetMetalParts[partNumber] += totalQuantity;
                            else
                                sheetMetalParts.Add(partNumber, totalQuantity);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in simple BOM row processing: {ex.Message}");
        }
    }

    private bool ShouldExcludeComponent(BOMStructureEnum bomStructure, string fullFileName, ScanOptions options)
    {
        if (options.ExcludeReferenceParts && bomStructure == BOMStructureEnum.kReferenceBOMStructure)
            return true;

        if (options.ExcludePurchasedParts && bomStructure == BOMStructureEnum.kPurchasedBOMStructure)
            return true;

        if (options.ExcludePhantomParts && bomStructure == BOMStructureEnum.kPhantomBOMStructure)
            return true;

        if (!options.IncludeLibraryComponents && !string.IsNullOrEmpty(fullFileName) && _inventorManager.IsLibraryComponent(fullFileName))
            return true;

        return false;
    }

    private static string GetFullFileName(ComponentOccurrence occ)
    {
        string fullFileName = "";
        if (occ.DefinitionDocumentType == DocumentTypeEnum.kPartDocumentObject)
        {
            if (occ.Definition.Document is PartDocument partDoc)
                fullFileName = partDoc.FullFileName;
        }
        else if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
        {
            if (occ.Definition.Document is AssemblyDocument asmDoc)
                fullFileName = asmDoc.FullFileName;
        }
        return fullFileName;
    }

    public void ClearCaches()
    {
        _documentCache.ClearCache();
        _conflictAnalyzer.Clear();
    }
}

public class ScanResult
{
    public Dictionary<string, int> SheetMetalParts { get; set; } = [];
    public int ProcessedCount { get; set; }
    public int SkippedCount { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public bool WasCancelled { get; set; }
    public bool HasMissingReferences { get; set; }
    public ProcessingMethod ProcessingMethod { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<AssemblyHierarchyNode> HierarchyRoots { get; set; } = [];
    public List<PartOccurrenceContribution> HierarchyContributions { get; set; } = [];
}

public class ScanOptions
{
    public bool ExcludeReferenceParts { get; set; }
    public bool ExcludePurchasedParts { get; set; }
    public bool ExcludePhantomParts { get; set; }
    public bool IncludeLibraryComponents { get; set; }
}
