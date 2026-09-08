using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Models;
using FlatPatternExporter.Services;
using FlatPatternExporter.UI.Windows;
using FlatPatternExporter.Utilities;
using Inventor;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace FlatPatternExporter.Core;

public class DxfExporter
{
    private readonly InventorManager _inventorManager;
    private readonly DocumentScanner _documentScanner;
    private readonly DocumentCache _documentCache;
    private readonly TokenService _tokenService;

    public DxfExporter(InventorManager inventorManager, DocumentScanner documentScanner,
        DocumentCache documentCache, TokenService tokenService)
    {
        _inventorManager = inventorManager;
        _documentScanner = documentScanner;
        _documentCache = documentCache;
        _tokenService = tokenService;
    }

    public async Task<ExportContext> PrepareExportContextAsync(
        Document document,
        bool requireScan,
        bool showProgress,
        Document? lastScannedDocument,
        ExportOptions exportOptions)
    {
        var context = new ExportContext();

        try
        {
            if (requireScan && document != lastScannedDocument)
            {
                _documentScanner.ConflictAnalyzer.Clear();
            }

            if (requireScan && document != lastScannedDocument)
            {
                context.IsValid = false;
                context.ErrorMessage = LocalizationManager.Instance.GetString("Error_DocumentChanged");
                return context;
            }

            if (!PrepareForExport(exportOptions, out var targetDir, out var multiplier))
            {
                context.IsValid = false;
                context.ErrorMessage = LocalizationManager.Instance.GetString("Error_ExportPreparation");
                return context;
            }

            context.TargetDirectory = targetDir;
            context.Multiplier = multiplier;

            var scanOptions = new ScanOptions
            {
                ExcludeReferenceParts = exportOptions.ExcludeReferenceParts,
                ExcludePurchasedParts = exportOptions.ExcludePurchasedParts,
                ExcludePhantomParts = exportOptions.ExcludePhantomParts,
                IncludeLibraryComponents = exportOptions.IncludeLibraryComponents
            };

            var scanResult = await _documentScanner.ScanDocumentAsync(
                document,
                exportOptions.SelectedProcessingMethod,
                scanOptions,
                showProgress ? new Progress<ScanProgress>() : null);

            context.SheetMetalParts = scanResult.SheetMetalParts;
            context.IsValid = true;
        }
        catch (Exception ex)
        {
            context.IsValid = false;
            context.ErrorMessage = LocalizationManager.Instance.GetString("Error_ExportPreparationWithMessage", ex.Message);
        }

        return context;
    }

    private bool PrepareForExport(ExportOptions exportOptions, out string targetDir, out int multiplier)
    {
        targetDir = "";
        multiplier = 1;

        switch (exportOptions.SelectedExportFolder)
        {
            case ExportFolderType.ChooseFolder:
                var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                    targetDir = dialog.SelectedPath;
                else
                    return false;
                break;

            case ExportFolderType.ComponentFolder:
                targetDir = Path.GetDirectoryName(_inventorManager.Application?.ActiveDocument?.FullFileName) ?? "";
                break;

            case ExportFolderType.FixedFolder:
                if (string.IsNullOrEmpty(exportOptions.FixedFolderPath))
                {
                    return false;
                }
                targetDir = exportOptions.FixedFolderPath;
                break;

            case ExportFolderType.ProjectFolder:
                try
                {
                    targetDir = _inventorManager.Application?.DesignProjectManager?.ActiveDesignProject?.WorkspacePath ?? "";
                    if (string.IsNullOrEmpty(targetDir))
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
                break;

            case ExportFolderType.PartFolder:
                break;
        }

        if (exportOptions.EnableSubfolder && !string.IsNullOrEmpty(exportOptions.SubfolderName))
        {
            targetDir = Path.Combine(targetDir!, exportOptions.SubfolderName);
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
        }

        multiplier = exportOptions.Multiplier;
        return true;
    }

    public void ExportDXF(
        IEnumerable<PartData> partsDataList,
        string targetDir,
        int multiplier,
        ExportOptions exportOptions,
        ref int processedCount,
        ref int skippedCount,
        bool generateThumbnails,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        var totalParts = partsDataList.Count();
        progress?.Report(0);

        var localProcessedCount = processedCount;
        var localSkippedCount = skippedCount;

        var thumbnailGenerator = generateThumbnails ? new ThumbnailGenerator() : null;
        var reservedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var partData in partsDataList)
        {
            partData.ProcessingStatusEnum = ProcessingStatus.Pending;
            partData.CutLengthMm = null;
        }

        try
        {
            foreach (var partData in partsDataList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var partNumber = partData.PartNumber;
                var qty = partData.IsOverridden ? partData.Quantity : partData.OriginalQuantity * multiplier;

                if (exportOptions.SelectedExportFolder == ExportFolderType.PartFolder)
                {
                    var partPath = _documentCache.GetCachedPartPath(partNumber) ?? _inventorManager.GetPartDocumentFullPath(partNumber);
                    if (!string.IsNullOrEmpty(partPath))
                    {
                        targetDir = Path.GetDirectoryName(partPath) ?? "";
                    }
                    else
                    {
                        targetDir = "";
                    }
                }

                PartDocument? partDoc = null;
                try
                {
                    partDoc = _documentCache.GetCachedPartDocument(partNumber) ?? _inventorManager.OpenPartDocument(partNumber);
                    if (partDoc == null) throw new Exception(LocalizationManager.Instance.GetString("Error_PartFileNotFound"));

                    var physicalProperties = PhysicalPropertiesCalculator.Calculate(partDoc);
                    partData.Mass = physicalProperties.FormattedMass;
                    partData.Density = physicalProperties.FormattedDensity;
                    partData.OnPropertyChanged(nameof(PartData.Mass));
                    partData.OnPropertyChanged(nameof(PartData.Density));

                    var smCompDef = (SheetMetalComponentDefinition)partDoc.ComponentDefinition;
                    var material = partData.Material;
                    var thickness = partData.Thickness;

                    var materialDir = exportOptions.OrganizeByMaterial ? Path.Combine(targetDir, material) : targetDir;
                    if (!Directory.Exists(materialDir)) Directory.CreateDirectory(materialDir);

                    var thicknessDir = exportOptions.OrganizeByThickness
                        ? Path.Combine(materialDir, thickness)
                        : materialDir;
                    if (!Directory.Exists(thicknessDir)) Directory.CreateDirectory(thicknessDir);

                    string fileName;
                    if (exportOptions.EnableFileNameConstructor && !string.IsNullOrEmpty(_tokenService.FileNameTemplate))
                    {
                        fileName = _tokenService.ResolveTemplate(_tokenService.FileNameTemplate, partData);
                    }
                    else
                    {
                        fileName = partNumber;
                    }

                    var filePath = MakeUniqueOutputPath(Path.Combine(thicknessDir, fileName + ".dxf"), reservedOutputPaths);

                    if (!IsValidPath(filePath)) continue;

                    var exportSuccess = false;

                    try
                    {
                        var flatPattern = GetOrCreateFlatPattern(smCompDef, partData);
                        UpdateBendMetrics(smCompDef, partData);
                        var oDataIO = flatPattern.DataIO;

                        var options = PrepareExportOptions(exportOptions);

                        var layerOptionsBuilder = new StringBuilder();
                        var invisibleLayersBuilder = new StringBuilder();

                        foreach (var layer in exportOptions.LayerSettings)
                        {
                            // Annotation entities are created after the Inventor translator has written the DXF.
                            if (layer.DisplayName == "BendAnnotationLayer") continue;

                            if (layer.CanBeHidden && !layer.IsChecked)
                            {
                                invisibleLayersBuilder.Append($"{layer.LayerName};");
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(layer.CustomName))
                                layerOptionsBuilder.Append($"&{layer.DisplayName}={layer.CustomName}");

                            if (layer.SelectedLineType != "Default")
                                layerOptionsBuilder.Append(
                                    $"&{layer.DisplayName}LineType={LayerSettingsHelper.GetLineTypeValue(layer.SelectedLineType)}");

                            if (layer.SelectedColor != "White")
                                layerOptionsBuilder.Append(
                                    $"&{layer.DisplayName}Color={LayerSettingsHelper.GetColorValue(layer.SelectedColor)}");
                        }

                        if (invisibleLayersBuilder.Length > 0)
                        {
                            invisibleLayersBuilder.Length -= 1;
                            layerOptionsBuilder.Append($"&InvisibleLayers={invisibleLayersBuilder}");
                        }

                        options += layerOptionsBuilder.ToString();

                        var dxfOptions = $"FLAT PATTERN DXF?{options}";

                        Debug.WriteLine($"DXF Options: {dxfOptions}");

                        if (!File.Exists(filePath)) File.Create(filePath).Close();

                        if (IsFileLocked(filePath, exportOptions, cancellationToken))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        var originalFlipBaseFace = ApplyTopSideMode(flatPattern, exportOptions.TopSideMode);
                        IReadOnlyList<BendAnnotationSource> bendAnnotations;
                        try
                        {
                            if (originalFlipBaseFace.HasValue) partDoc.Update2(false);
                            bendAnnotations = IsBendAnnotationEnabled(exportOptions)
                                ? CaptureBendAnnotations(flatPattern, partData.DocumentLengthUnit)
                                : [];
                            oDataIO.WriteDataToFile(dxfOptions, filePath);
                        }
                        finally
                        {
                            if (originalFlipBaseFace.HasValue)
                            {
                                flatPattern.FlatPatternOrientations.ActiveFlatPatternOrientation.FlipBaseFace = originalFlipBaseFace.Value;
                                partDoc.Update2(false);
                            }
                        }
                        var postProcessResult = DxfPostProcessor.Process(filePath, new DxfPostProcessOptions
                        {
                            OptimizeVersion = exportOptions.OptimizeDxf,
                            TargetVersion = exportOptions.SelectedAcadVersion,
                            ConvertSplinesToFitPoints = exportOptions.EnableSplineReplacement &&
                                exportOptions.SelectedSplineReplacement == SplineReplacementType.FitPoints,
                            SplineTolerance = exportOptions.EnableSplineReplacement &&
                                exportOptions.SelectedSplineReplacement == SplineReplacementType.FitPoints
                                    ? ParseSplineTolerance(exportOptions.SplineTolerance)
                                    : 0.01,
                            CuttingLayers = GetCuttingLayers(exportOptions.LayerSettings),
                            DocumentUnit = partData.DocumentLengthUnit,
                            BendAnnotations = CreateBendAnnotationOptions(exportOptions, bendAnnotations)
                        });
                        partData.CutLengthMm = postProcessResult.CutLengthMm;
                        exportSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DXF export error: {ex.Message}");
                    }

                    partData.ProcessingStatusEnum = exportSuccess ? ProcessingStatus.Success : ProcessingStatus.Skipped;

                    if (exportSuccess && generateThumbnails && thumbnailGenerator != null &&
                        AcadVersionMapping.SupportsOptimization(exportOptions.SelectedAcadVersion))
                    {
                        var dxfPreview = thumbnailGenerator.GenerateDxfThumbnails(filePath, Dispatcher.CurrentDispatcher);
                        if (dxfPreview != null)
                        {
                            partData.DxfPreview = dxfPreview;
                        }
                    }

                    if (exportSuccess)
                        localProcessedCount++;
                    else
                        localSkippedCount++;

                    progress?.Report(totalParts > 0 ? (double)localProcessedCount / totalParts * 100 : 0);
                }
                catch (Exception ex)
                {
                    localSkippedCount++;
                    Debug.WriteLine($"Part processing error: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var partData in partsDataList)
            {
                if (partData.ProcessingStatusEnum == ProcessingStatus.Pending)
                {
                    partData.ProcessingStatusEnum = ProcessingStatus.Interrupted;
                }
            }
            throw;
        }

        processedCount = localProcessedCount;
        skippedCount = localSkippedCount;

        progress?.Report(100);
    }

    private static FlatPattern GetOrCreateFlatPattern(SheetMetalComponentDefinition componentDefinition, PartData partData)
    {
        var created = !componentDefinition.HasFlatPattern;
        if (created) componentDefinition.Unfold();

        if (!componentDefinition.HasFlatPattern)
            throw new InvalidOperationException(LocalizationManager.Instance.GetString("Error_FlatPatternCreationFailed"));

        var flatPattern = componentDefinition.FlatPattern;
        if (created)
        {
            try
            {
                flatPattern.ExitEdit();
            }
            catch
            {
                // Some document states create the flat pattern without entering edit mode.
            }

            partData.HasFlatPattern = true;
        }

        return flatPattern;
    }

    private static void UpdateBendMetrics(SheetMetalComponentDefinition definition, PartData partData)
    {
        var metrics = SheetMetalBendAnalyzer.Analyze(definition);
        partData.BendCount = metrics.Count;
        partData.LongestBendLengthMm = metrics.LongestLengthMm;
        partData.BendsUpCount = metrics.UpCount;
        partData.BendsDownCount = metrics.DownCount;
        partData.OnPropertyChanged(nameof(PartData.BendCount));
        partData.OnPropertyChanged(nameof(PartData.LongestBendLengthMm));
        partData.OnPropertyChanged(nameof(PartData.BendsUpCount));
        partData.OnPropertyChanged(nameof(PartData.BendsDownCount));
    }

    private static bool? ApplyTopSideMode(FlatPattern flatPattern, FlatPatternTopSideMode mode)
    {
        if (mode == FlatPatternTopSideMode.AsModeled) return null;

        var results = flatPattern.FlatBendResults;
        var upCount = 0;
        var downCount = 0;
        foreach (FlatBendResult result in results)
        {
            if (result.IsOnBottomFace) continue;
            if (result.IsDirectionUp) upCount++;
            else downCount++;
        }

        var shouldFlip = mode switch
        {
            FlatPatternTopSideMode.MostBendsUp => downCount > upCount,
            FlatPatternTopSideMode.MostBendsDown => upCount > downCount,
            _ => false
        };
        if (!shouldFlip) return null;

        var orientation = flatPattern.FlatPatternOrientations.ActiveFlatPatternOrientation;
        var original = orientation.FlipBaseFace;
        orientation.FlipBaseFace = !original;
        return original;
    }

    private static IReadOnlyList<BendAnnotationSource> CaptureBendAnnotations(
        FlatPattern flatPattern,
        DocumentLengthUnit documentUnit)
    {
        var annotations = new List<BendAnnotationSource>();
        foreach (FlatBendResult result in flatPattern.FlatBendResults)
        {
            if (result.IsOnBottomFace) continue;

            try
            {
                var evaluator = result.Edge.Evaluator;
                evaluator.GetParamExtents(out var minimumParameter, out var maximumParameter);
                evaluator.GetLengthAtParam(minimumParameter, maximumParameter, out var lengthCentimeters);
                annotations.Add(new BendAnnotationSource(
                    LengthUnitConverter.FromCentimeters(lengthCentimeters, documentUnit),
                    Math.Abs(result.Angle * 180.0 / Math.PI),
                    LengthUnitConverter.FromCentimeters(result.InnerRadius, documentUnit),
                    result.IsDirectionUp));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cannot read bend annotation data: {ex.Message}");
            }
        }

        return annotations;
    }

    private static bool IsBendAnnotationEnabled(ExportOptions exportOptions) =>
        exportOptions.LayerSettings.Any(layer => layer.DisplayName == "BendAnnotationLayer" && layer.IsChecked);

    private static BendAnnotationRenderOptions? CreateBendAnnotationOptions(
        ExportOptions exportOptions,
        IReadOnlyList<BendAnnotationSource> sources)
    {
        var annotationLayer = exportOptions.LayerSettings.FirstOrDefault(layer =>
            layer.DisplayName == "BendAnnotationLayer");
        if (annotationLayer is null || !annotationLayer.IsChecked || sources.Count == 0) return null;

        if (!TryParsePositiveNumber(exportOptions.BendAnnotationTextHeight, out var textHeight)) return null;

        return new BendAnnotationRenderOptions(
            exportOptions.BendAnnotationTemplate,
            string.IsNullOrWhiteSpace(exportOptions.BendAnnotationFontFamily)
                ? "Arial"
                : exportOptions.BendAnnotationFontFamily,
            textHeight,
            exportOptions.ConvertBendAnnotationsToCurves,
            ResolveLayerName(annotationLayer),
            annotationLayer.SelectedColor,
            ResolveLayerName(exportOptions.LayerSettings.FirstOrDefault(layer => layer.DisplayName == "BendUpLayer")!, "IV_BEND"),
            ResolveLayerName(exportOptions.LayerSettings.FirstOrDefault(layer => layer.DisplayName == "BendDownLayer")!, "IV_BEND_DOWN"),
            sources);
    }

    private static bool TryParsePositiveNumber(string value, out double number)
    {
        var parsed = double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number) ||
                     double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        return parsed && double.IsFinite(number) && number > 0;
    }

    private static string ResolveLayerName(LayerSetting? layer, string fallback = "IV_BEND_TEXT") =>
        layer is null || string.IsNullOrWhiteSpace(layer.CustomName) ? layer?.LayerName ?? fallback : layer.CustomName;

    private string PrepareExportOptions(ExportOptions exportOptions)
    {
        var sb = new StringBuilder();

        sb.Append($"AcadVersion={AcadVersionMapping.GetTranslatorCode(exportOptions.SelectedAcadVersion)}");

        if (exportOptions.EnableSplineReplacement)
        {
            var splineTolerance = exportOptions.SplineTolerance;

            var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
            splineTolerance = splineTolerance.Replace('.', decimalSeparator).Replace(',', decimalSeparator);

            if (exportOptions.SelectedSplineReplacement == SplineReplacementType.Lines)
                sb.Append($"&SimplifySplines=True&SplineTolerance={splineTolerance}");
            else if (exportOptions.SelectedSplineReplacement == SplineReplacementType.Arcs)
                sb.Append($"&SimplifySplines=True&SimplifyAsTangentArcs=True&SplineTolerance={splineTolerance}");
            else
                sb.Append("&SimplifySplines=False");
        }
        else
        {
            sb.Append("&SimplifySplines=False");
        }

        if (exportOptions.MergeProfilesIntoPolyline) sb.Append("&MergeProfilesIntoPolyline=True");

        if (exportOptions.RebaseGeometry) sb.Append("&RebaseGeometry=True");

        if (exportOptions.TrimCenterlines)
            sb.Append("&TrimCenterlinesAtContour=True");

        return sb.ToString();
    }

    private static double ParseSplineTolerance(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var tolerance) ||
            double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out tolerance))
            return tolerance;

        throw new FormatException($"Invalid spline tolerance: '{value}'.");
    }

    private static IReadOnlySet<string> GetCuttingLayers(IEnumerable<LayerSetting> layerSettings) =>
        layerSettings
            .Where(layer => layer.DisplayName is "OuterProfileLayer" or "InteriorProfilesLayer")
            .Where(layer => !layer.CanBeHidden || layer.IsChecked)
            .Select(layer => string.IsNullOrWhiteSpace(layer.CustomName) ? layer.LayerName : layer.CustomName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private bool IsFileLocked(string filePath, ExportOptions exportOptions, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            stream.Close();
            return false;
        }
        catch (IOException)
        {
            if (exportOptions.ShowFileLockedDialogs)
            {
                var result = CustomMessageBox.Show(
                    LocalizationManager.Instance.GetString("Warning_FileLocked", filePath),
                    LocalizationManager.Instance.GetString("MessageBox_Warning"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    throw new OperationCanceledException();
                }

                while (IsFileLockedInternal(filePath))
                {
                    var waitResult = CustomMessageBox.Show(
                        LocalizationManager.Instance.GetString("Info_WaitingForFileUnlock"),
                        LocalizationManager.Instance.GetString("MessageBox_Info"),
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Information);

                    if (waitResult == MessageBoxResult.Cancel)
                    {
                        throw new OperationCanceledException();
                    }

                    Thread.Sleep(1000);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            return true;
        }
    }

    internal static string MakeUniqueOutputPath(string filePath, ISet<string> reservedPaths)
    {
        if (reservedPaths.Add(filePath)) return filePath;

        var directory = Path.GetDirectoryName(filePath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        for (var suffix = 2; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{baseName}_{suffix}{extension}");
            if (reservedPaths.Add(candidate)) return candidate;
        }
    }

    private static bool IsFileLockedInternal(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            stream.Close();
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static bool IsValidPath(string path)
    {
        try
        {
            _ = new FileInfo(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class ExportContext
{
    public string TargetDirectory { get; set; } = "";
    public int Multiplier { get; set; } = 1;
    public Dictionary<string, int> SheetMetalParts { get; set; } = [];
    public bool GenerateThumbnails { get; set; } = true;
    public bool IsValid { get; set; } = true;
    public string ErrorMessage { get; set; } = "";
}

public class ExportOptions
{
    public ExportFolderType SelectedExportFolder { get; set; }
    public string FixedFolderPath { get; set; } = "";
    public bool EnableSubfolder { get; set; }
    public string SubfolderName { get; set; } = "";
    public int Multiplier { get; set; } = 1;
    public ProcessingMethod SelectedProcessingMethod { get; set; }

    public bool ExcludeReferenceParts { get; set; }
    public bool ExcludePurchasedParts { get; set; }
    public bool ExcludePhantomParts { get; set; }
    public bool IncludeLibraryComponents { get; set; }

    public bool OrganizeByMaterial { get; set; }
    public bool OrganizeByThickness { get; set; }
    public bool EnableFileNameConstructor { get; set; }

    public bool OptimizeDxf { get; set; }
    public bool EnableSplineReplacement { get; set; }
    public SplineReplacementType SelectedSplineReplacement { get; set; }
    public string SplineTolerance { get; set; } = "0.01";
    public AcadVersionType SelectedAcadVersion { get; set; }
    public bool MergeProfilesIntoPolyline { get; set; }
    public bool RebaseGeometry { get; set; }
    public bool TrimCenterlines { get; set; }
    public FlatPatternTopSideMode TopSideMode { get; set; } = FlatPatternTopSideMode.AsModeled;
    public string BendAnnotationTemplate { get; set; } = "{Direction} {Angle}° R{Radius} L{Length}";
    public string BendAnnotationFontFamily { get; set; } = "Arial";
    public string BendAnnotationTextHeight { get; set; } = "3";
    public bool ConvertBendAnnotationsToCurves { get; set; }

    public List<LayerSetting> LayerSettings { get; set; } = [];
    public bool ShowFileLockedDialogs { get; set; } = true;
}
