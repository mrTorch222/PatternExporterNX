using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FlatPatternExporter.Core;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Features.Frame.Core;
using FlatPatternExporter.Features.Frame.Models;
using FlatPatternExporter.Features.Frame.Services;
using FlatPatternExporter.Models;
using FlatPatternExporter.Services;
using FlatPatternExporter.UI.Windows;
using Inventor;
using IOPath = System.IO.Path;
using WpfBorder = System.Windows.Controls.Border;
using WpfBrush = System.Windows.Media.Brush;
using WpfStyle = System.Windows.Style;

namespace FlatPatternExporter.Features.Frame.UI;

public partial class FrameExporterControl : System.Windows.Controls.UserControl
{
    private static readonly string[] DefaultAttributeColumns = ["PartNumber", "StockNumber", "Material", "Description"];
    private static readonly Regex TemplateSegmentRegex = new(
        @"\{CUSTOM:(?<custom>[^}]*)\}|\{(?<token>[^{}:]+)\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TrailingCustomTextRegex = new(
        @"\{CUSTOM:(?<custom>[^}]*)\}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ObservableCollection<FrameMemberData> _members = [];
    private readonly ICollectionView _membersView;
    private readonly Dictionary<string, PartDocument> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly LocalizationManager _localization = LocalizationManager.Instance;
    private readonly TemplatePresetManager _presetManager = new();
    private readonly ObservableCollection<FrameNameTokenDefinition> _availableNameTokens = [];
    private readonly ObservableCollection<FrameNameTokenDefinition> _userDefinedNameTokens = [];
    private InventorManager? _inventorManager;
    private string _fileNameTemplate = FrameFileNameService.DefaultTemplate;
    private CancellationTokenSource? _operationCancellation;
    private bool _isBusy;
    private bool _isUpdatingPresetState;

    public FrameExporterControl()
    {
        InitializeComponent();
        _membersView = CollectionViewSource.GetDefaultView(_members);
        _membersView.Filter = FilterFrameMember;
        FrameMembersGrid.ItemsSource = _membersView;
        FrameTemplatePresetsListBox.ItemsSource = _presetManager.TemplatePresets;
        FrameAvailableTokensListBox.ItemsSource = _availableNameTokens;
        FrameUserDefinedTokensListBox.ItemsSource = _userDefinedNameTokens;
        _presetManager.TemplatePresets.CollectionChanged += (_, _) => UpdatePresetControls();
        PropertyMetadataRegistry.UserDefinedProperties.CollectionChanged += (_, _) => RefreshAvailableNameTokens();
        _localization.LanguageChanged += (_, _) =>
        {
            RefreshAvailableNameTokens();
            RefreshAttributeColumnHeaders();
            RenderFileNameTemplate();
            UpdatePreview();
        };
        RefreshAvailableNameTokens();
        RenderFileNameTemplate();
        UpdatePresetControls();
        UpdatePreview();
        _localization.LanguageChanged += (_, _) =>
        {
            foreach (var member in _members) member.RefreshLocalization();
        };
    }

    public void Initialize(InventorManager inventorManager)
    {
        _inventorManager = inventorManager;
    }

    public void CancelActiveOperation() => _operationCancellation?.Cancel();

    public void ApplySettings(FrameExportSettings? settings)
    {
        settings ??= new FrameExportSettings();
        OutputFolderTextBox.Text = string.IsNullOrWhiteSpace(settings.FixedFolderPath)
            ? settings.OutputFolder
            : settings.FixedFolderPath;
        SetSelectedExportFolder(settings.SelectedExportFolder
            ?? (string.IsNullOrWhiteSpace(OutputFolderTextBox.Text) ? ExportFolderType.ChooseFolder : ExportFolderType.FixedFolder));
        FrameEnableSubfolderCheckBox.IsChecked = settings.EnableSubfolder;
        FrameSubfolderNameTextBox.Text = settings.SubfolderName;
        FrameOrganizeByMaterialCheckBox.IsChecked = settings.OrganizeByMaterial;
        FrameOrganizeByStockNumberCheckBox.IsChecked = settings.OrganizeByStockNumber;
        FrameCsvDelimiterComboBox.SelectedItem = settings.CsvDelimiter;
        FrameBomFileNameComboBox.SelectedItem = settings.BomFileNameType;
        FrameExcelFormatRadioButton.IsChecked = settings.DefaultBomFormat == ExportFileFormat.Excel;
        FrameCsvFormatRadioButton.IsChecked = settings.DefaultBomFormat == ExportFileFormat.Csv;
        RestoreAttributeColumns(settings.AttributeColumnOrder);
        GeometryTypeComboBox.SelectedIndex = ClampIndex(settings.GeometryType, GeometryTypeComboBox.Items.Count, 0);
        SolidFaceTypeComboBox.SelectedIndex = ClampIndex(settings.SolidFaceType, SolidFaceTypeComboBox.Items.Count, 1);
        SurfaceTypeComboBox.SelectedIndex = ClampIndex(settings.SurfaceType, SurfaceTypeComboBox.Items.Count, 1);
        ExportFormatComboBox.SelectedIndex = ClampIndex((int)settings.ExportFormat, ExportFormatComboBox.Items.Count, 0);
        FrameEnableFileNameConstructorCheckBox.IsChecked = settings.EnableFileNameConstructor;
        _presetManager.LoadPresets(settings.TemplatePresets, settings.SelectedTemplatePresetIndex);
        FrameTemplatePresetsListBox.SelectedItem = _presetManager.SelectedTemplatePreset;
        SetFileNameTemplate(string.IsNullOrWhiteSpace(settings.FileNameTemplate)
            ? FrameFileNameService.DefaultTemplate
            : settings.FileNameTemplate);
        UpdateFormatControls();
        UpdateExportPlacementControls();
        UpdateBomFormatControls();
        UpdatePresetControls();
        UpdatePreview();
    }

    public FrameExportSettings CollectSettings() => new()
    {
        OutputFolder = OutputFolderTextBox.Text.Trim(),
        SelectedExportFolder = GetSelectedExportFolder(),
        FixedFolderPath = OutputFolderTextBox.Text.Trim(),
        EnableSubfolder = FrameEnableSubfolderCheckBox.IsChecked == true,
        SubfolderName = FrameSubfolderNameTextBox.Text.Trim(),
        OrganizeByMaterial = FrameOrganizeByMaterialCheckBox.IsChecked == true,
        OrganizeByStockNumber = FrameOrganizeByStockNumberCheckBox.IsChecked == true,
        CsvDelimiter = FrameCsvDelimiterComboBox.SelectedItem is CsvDelimiterType delimiter ? delimiter : CsvDelimiterType.Tab,
        DefaultBomFormat = FrameCsvFormatRadioButton.IsChecked == true ? ExportFileFormat.Csv : ExportFileFormat.Excel,
        BomFileNameType = FrameBomFileNameComboBox.SelectedItem is ExcelExportFileNameType fileNameType
            ? fileNameType
            : ExcelExportFileNameType.DateTimeFormat,
        AttributeColumnOrder = FrameMembersGrid.Columns
            .Where(column => GetAttributeInternalName(column) is not null)
            .OrderBy(column => column.DisplayIndex)
            .Select(column => GetAttributeInternalName(column)!)
            .ToList(),
        EnableFileNameConstructor = FrameEnableFileNameConstructorCheckBox.IsChecked == true,
        FileNameTemplate = _fileNameTemplate,
        GeometryType = GeometryTypeComboBox.SelectedIndex,
        SolidFaceType = SolidFaceTypeComboBox.SelectedIndex,
        SurfaceType = SurfaceTypeComboBox.SelectedIndex,
        ExportFormat = (FrameExportFormat)ExportFormatComboBox.SelectedIndex,
        TemplatePresets = _presetManager.GetPresetData().ToList(),
        SelectedTemplatePresetIndex = _presetManager.GetSelectedPresetIndex()
    };

    private async void ScanFrameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _inventorManager is null) return;

        try
        {
            _operationCancellation = new CancellationTokenSource();
            var cancellationToken = _operationCancellation.Token;
            SetBusy(true);
            StatusTextBlock.Text = _localization.GetString("Frame_StatusScanning");

            var validation = _inventorManager.ValidateActiveDocument();
            if (!validation.IsValid || validation.Document is not AssemblyDocument assemblyDocument)
            {
                ShowError(_localization.GetString("Frame_ErrorAssemblyRequired"));
                return;
            }

            var requestedAttributes = GetRequestedAttributes();
            var result = await StaTaskRunner.RunAsync(
                () => new FrameMemberScanner().Scan(assemblyDocument, requestedAttributes, cancellationToken),
                cancellationToken);
            _members.Clear();
            _documents.Clear();
            foreach (var member in result.Members) _members.Add(member);
            foreach (var pair in result.Documents) _documents.Add(pair.Key, pair.Value);

            SetDefaultOutputFolder(assemblyDocument);
            StatusTextBlock.Text = _localization.GetString("Frame_StatusScanComplete", _members.Count, result.Errors.Count);
            if (_members.Count == 0) ShowError(_localization.GetString("Frame_InfoNoMembers"));
            else if (result.Errors.Count > 0) ShowErrors(result.Errors);
            UpdatePreview();
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = _localization.GetString("Frame_StatusCancelled");
        }
        catch (Exception ex)
        {
            ShowError(_localization.GetString("Frame_Error", ex.Message));
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private async void Export3dButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _inventorManager is null || _members.Count == 0) return;

        if (!TryResolveExportFolder(out var outputFolder, out var usePartFolder)) return;
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            ShowError(_localization.GetString("Frame_ErrorOutputFolder"));
            return;
        }

        try
        {
            _operationCancellation = new CancellationTokenSource();
            var cancellationToken = _operationCancellation.Token;
            SetBusy(true);
            StatusTextBlock.Text = _localization.GetString("Frame_StatusExporting");
            var effectiveTemplate = FrameEnableFileNameConstructorCheckBox.IsChecked == true
                ? _fileNameTemplate
                : FrameFileNameService.FallbackTemplate;
            if (!FrameFileNameService.ValidateTemplate(effectiveTemplate))
            {
                ShowError(_localization.GetString("Error_UnknownTokens"));
                return;
            }
            var options = new FrameExportOptions(
                outputFolder,
                effectiveTemplate,
                GeometryTypeComboBox.SelectedIndex,
                SolidFaceTypeComboBox.SelectedIndex,
                SurfaceTypeComboBox.SelectedIndex,
                (FrameExportFormat)ExportFormatComboBox.SelectedIndex,
                usePartFolder,
                FrameEnableSubfolderCheckBox.IsChecked == true,
                FrameSubfolderNameTextBox.Text.Trim(),
                FrameOrganizeByMaterialCheckBox.IsChecked == true,
                FrameOrganizeByStockNumberCheckBox.IsChecked == true);
            var exporter = new Frame3dExporter(_inventorManager);
            foreach (var member in _members)
            {
                member.ProcessingStatus = ProcessingStatus.Pending;
                member.OutputFile = "";
            }
            var progress = new Progress<FrameExportProgress>(value =>
                StatusTextBlock.Text = _localization.GetString(
                    "Frame_StatusExportProgress",
                    value.Completed,
                    value.Total,
                    value.MemberName));
            var result = await StaTaskRunner.RunAsync(
                () => exporter.Export(_members, _documents, options, cancellationToken, progress),
                cancellationToken);
            foreach (var item in result.Items)
            {
                var member = _members.FirstOrDefault(candidate => candidate.DocumentKey == item.DocumentKey);
                if (member is null) continue;
                member.OutputFile = item.OutputFile;
                member.ProcessingStatus = item.Status;
            }
            if (result.WasCancelled)
            {
                foreach (var member in _members.Where(item => item.ProcessingStatus == ProcessingStatus.Pending))
                    member.ProcessingStatus = ProcessingStatus.Interrupted;
                StatusTextBlock.Text = _localization.GetString("Frame_StatusCancelled");
                return;
            }
            StatusTextBlock.Text = _localization.GetString("Frame_StatusExportComplete", result.ExportedCount, result.Errors.Count);
            if (result.Errors.Count > 0) ShowErrors(result.Errors);
        }
        catch (OperationCanceledException)
        {
            foreach (var member in _members.Where(item => item.ProcessingStatus == ProcessingStatus.Pending))
                member.ProcessingStatus = ProcessingStatus.Interrupted;
            StatusTextBlock.Text = _localization.GetString("Frame_StatusCancelled");
        }
        catch (Exception ex)
        {
            ShowError(_localization.GetString("Frame_Error", ex.Message));
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private void ExportBomButton_Click(object sender, RoutedEventArgs e)
    {
        if (_members.Count == 0) return;

        var format = FrameCsvFormatRadioButton.IsChecked == true ? ExportFileFormat.Csv : ExportFileFormat.Excel;
        var extension = ExportFileFormatMapping.GetFileExtension(format);
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = _localization.GetString("Frame_ButtonExportBom"),
            Filter = _localization.GetString("Frame_BomFileFilter"),
            DefaultExt = extension,
            FileName = GetBomExportFileName() + extension,
            AddExtension = true,
            FilterIndex = ExportFileFormatMapping.GetFilterIndex(format)
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var columns = GetSelectedBomColumns();
            if (string.Equals(IOPath.GetExtension(dialog.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
                FrameBomExportService.ExportCsv(
                    dialog.FileName,
                    _members,
                    CsvDelimiterMapping.GetDelimiter(FrameCsvDelimiterComboBox.SelectedItem is CsvDelimiterType delimiter
                        ? delimiter
                        : CsvDelimiterType.Tab),
                    columns);
            else
                FrameBomExportService.ExportExcel(dialog.FileName, _members, columns);
            StatusTextBlock.Text = _localization.GetString("Frame_StatusBomExported", dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowError(_localization.GetString("Frame_Error", ex.Message));
        }
    }

    private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _localization.GetString("Frame_SelectOutputFolder"),
            SelectedPath = Directory.Exists(OutputFolderTextBox.Text) ? OutputFolderTextBox.Text : ""
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            OutputFolderTextBox.Text = dialog.SelectedPath;
    }

    private void FrameExportPlacement_Changed(object sender, RoutedEventArgs e) => UpdateExportPlacementControls();

    private void FrameSubfolder_Changed(object sender, RoutedEventArgs e) => UpdateExportPlacementControls();

    private void FrameBomFormat_Changed(object sender, RoutedEventArgs e) => UpdateBomFormatControls();

    private void FrameSubfolderNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;
        var sanitized = string.Concat(textBox.Text.Where(character => !IOPath.GetInvalidFileNameChars().Contains(character)));
        if (sanitized == textBox.Text) return;
        var caretIndex = Math.Min(textBox.CaretIndex, sanitized.Length);
        textBox.Text = sanitized;
        textBox.CaretIndex = caretIndex;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _members.Clear();
        _documents.Clear();
        StatusTextBlock.Text = "";
        UpdateButtons();
        UpdatePreview();
    }

    private void CancelFrameButton_Click(object sender, RoutedEventArgs e)
    {
        CancelFrameButton.IsEnabled = false;
        _operationCancellation?.Cancel();
    }

    private HashSet<string> GetRequestedAttributes()
    {
        var attributes = FrameMembersGrid.Columns
            .Select(GetAttributeInternalName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (FrameEnableFileNameConstructorCheckBox.IsChecked != true) return attributes;

        var templateTokens = TemplateSegmentRegex.Matches(_fileNameTemplate)
            .Select(match => match.Groups["token"].Value)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var property in PropertyMetadataRegistry.UserDefinedProperties)
        {
            if (templateTokens.Contains(property.TokenName)) attributes.Add(property.InternalName);
        }

        return attributes;
    }

    private void FrameEnableFileNameConstructorCheckBox_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    private void FrameTokenListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: FrameNameTokenDefinition token })
            AddFileNameToken(token.TokenName);
    }

    private void AddFrameCustomTextButton_Click(object sender, RoutedEventArgs e)
    {
        AddCustomText(FrameCustomTextBox.Text);
        FrameCustomTextBox.Clear();
    }

    private void AddFrameSymbolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string symbol }) AddCustomText(symbol);
    }

    private void FrameCustomTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddFrameCustomTextButton_Click(sender, new RoutedEventArgs());
        e.Handled = true;
    }

    private void AddFrameUserPropertyButton_Click(object sender, RoutedEventArgs e)
    {
        var properties = GetFrameSelectableProperties();
        var window = new SelectIPropertyWindow(
            properties,
            _ => true,
            _ => { },
            _ => RefreshAvailableNameTokens(),
            (_, _) => RefreshAvailableNameTokens(),
            "UserProperties")
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
        RefreshMemberUserDefinedProperties();
        RefreshAvailableNameTokens();
        UpdatePreview();
    }

    private void RefreshMemberUserDefinedProperties()
    {
        foreach (var member in _members)
        {
            if (!_documents.TryGetValue(member.DocumentKey, out var document)) continue;
            var propertyManager = new FlatPatternExporter.Core.PropertyManager((Document)document);
            foreach (var property in PropertyMetadataRegistry.UserDefinedProperties)
            {
                if (property.InventorPropertyName is not { Length: > 0 } propertyName) continue;
                var value = propertyManager.GetMappedProperty(property.InternalName);
                member.UserDefinedProperties[propertyName] = value;
                member.SetAttributeValue(property.InternalName, value);
            }
        }
    }

    private void AddFrameColumnsButton_Click(object sender, RoutedEventArgs e)
    {
        var properties = GetFrameSelectableProperties();
        var window = new SelectIPropertyWindow(
            properties,
            internalName => FrameMembersGrid.Columns.Any(column => GetAttributeInternalName(column) == internalName),
            AddFrameAttributeColumn,
            AddFrameUserDefinedAttributeColumn,
            RemoveFrameAttributeColumn)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private void FrameSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (FrameClearSearchButton is null) return;
        FrameClearSearchButton.IsEnabled = !string.IsNullOrWhiteSpace(FrameSearchTextBox.Text);
        _membersView.Refresh();
    }

    private void FrameClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        FrameSearchTextBox.Clear();
        FrameSearchTextBox.Focus();
    }

    private bool FilterFrameMember(object item)
    {
        if (item is not FrameMemberData member) return false;
        var search = FrameSearchTextBox?.Text.Trim();
        if (string.IsNullOrWhiteSpace(search)) return true;

        return new[]
            {
                member.ProcessingStatusText,
                member.PartNumber,
                member.StockNumber,
                member.Material,
                member.Description,
                member.LengthMm?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? "",
                member.Quantity.ToString(System.Globalization.CultureInfo.CurrentCulture),
                member.FileName,
                member.OutputFile
            }
            .Concat(member.AttributeValues.Values)
            .Concat(member.UserDefinedProperties.Values)
            .Any(value => value.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    }

    private void RestoreAttributeColumns(IReadOnlyCollection<string>? columnOrder)
    {
        foreach (var column in FrameMembersGrid.Columns.Where(column => GetAttributeInternalName(column) is not null).ToList())
            FrameMembersGrid.Columns.Remove(column);

        var selectedColumns = columnOrder ?? DefaultAttributeColumns;
        foreach (var internalName in selectedColumns)
            AddFrameAttributeColumn(internalName);
    }

    private void AddFrameAttributeColumn(PresetIProperty property) => AddFrameAttributeColumn(property.InventorPropertyName);

    private void AddFrameAttributeColumn(string internalName)
    {
        if (FrameMembersGrid.Columns.Any(column => GetAttributeInternalName(column) == internalName)) return;
        var metadata = PropertyMetadataRegistry.GetPropertyByInternalName(internalName);
        if (metadata is null || metadata.Type is not (PropertyMetadataRegistry.PropertyType.IProperty or PropertyMetadataRegistry.PropertyType.UserDefined))
            return;

        FillFrameAttributeData(internalName);
        var binding = new Binding($"AttributeValues[{internalName}]");
        if (metadata.RequiresRounding) binding.StringFormat = $"F{metadata.RoundingDecimals}";
        var column = new DataGridTextColumn
        {
            Header = metadata.ColumnHeader,
            Binding = binding,
            SortMemberPath = $"AttributeValues[{internalName}]",
            CanUserSort = metadata.IsSortable,
            IsReadOnly = true,
            ElementStyle = FrameMembersGrid.FindResource("CenteredCellStyle") as WpfStyle
        };
        var insertionIndex = FrameMembersGrid.Columns
            .Select((existing, index) => (existing, index))
            .FirstOrDefault(item => item.existing.SortMemberPath == "LengthMm").index;
        FrameMembersGrid.Columns.Insert(Math.Max(1, insertionIndex), column);
    }

    private void AddFrameUserDefinedAttributeColumn(string propertyName)
    {
        PropertyMetadataRegistry.AddUserDefinedProperty(propertyName);
        AddFrameAttributeColumn($"UDP_{propertyName}");
        RefreshAvailableNameTokens();
    }

    private void RemoveFrameAttributeColumn(string internalName, bool _)
    {
        var column = FrameMembersGrid.Columns.FirstOrDefault(item => GetAttributeInternalName(item) == internalName);
        if (column is not null) FrameMembersGrid.Columns.Remove(column);
    }

    private void FillFrameAttributeData(string internalName)
    {
        foreach (var member in _members)
        {
            if (!_documents.TryGetValue(member.DocumentKey, out var document)) continue;
            var value = new FlatPatternExporter.Core.PropertyManager((Document)document).GetMappedProperty(internalName);
            member.SetAttributeValue(internalName, value);
            if (PropertyMetadataRegistry.IsUserDefinedProperty(internalName))
                member.UserDefinedProperties[PropertyMetadataRegistry.GetInventorNameFromUserDefinedInternalName(internalName)] = value;
        }
    }

    private void RefreshAttributeColumnHeaders()
    {
        foreach (var column in FrameMembersGrid.Columns)
        {
            var internalName = GetAttributeInternalName(column);
            if (internalName is not null)
                column.Header = PropertyMetadataRegistry.GetPropertyByInternalName(internalName)?.ColumnHeader ?? column.Header;
        }
    }

    private static string? GetAttributeInternalName(DataGridColumn column)
    {
        const string prefix = "AttributeValues[";
        var path = column.SortMemberPath;
        return path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(']')
            ? path[prefix.Length..^1]
            : null;
    }

    private static ObservableCollection<PresetIProperty> GetFrameSelectableProperties() => new(
        PropertyMetadataRegistry.Properties.Values
            .Where(property => property.Type == PropertyMetadataRegistry.PropertyType.IProperty)
            .OrderBy(property => property.Category)
            .ThenBy(property => property.DisplayName)
            .Select(property => new PresetIProperty { InventorPropertyName = property.InternalName }));

    private void FramePresetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPresetState) return;

        _presetManager.SelectedTemplatePreset = FrameTemplatePresetsListBox.SelectedItem as TemplatePreset;
        var selected = _presetManager.SelectedTemplatePreset;
        _isUpdatingPresetState = true;
        try
        {
            FramePresetNameTextBox.Text = selected?.Name ?? "";
            if (selected is not null) SetFileNameTemplate(selected.Template);
        }
        finally
        {
            _isUpdatingPresetState = false;
        }
        UpdatePresetControls();
    }

    private void CreateFramePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var name = FramePresetNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (!_presetManager.CreatePreset(name, _fileNameTemplate, out _)) return;
        FrameTemplatePresetsListBox.SelectedItem = _presetManager.SelectedTemplatePreset;
        UpdatePresetControls();
    }

    private void UpdateFramePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_presetManager.SelectedTemplatePreset is null) return;
        _presetManager.UpdateSelectedTemplate(_fileNameTemplate);
        FrameTemplatePresetsListBox.Items.Refresh();
        UpdatePresetControls();
    }

    private void RenameFramePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_presetManager.SelectedTemplatePreset is null) return;
        if (!_presetManager.RenameSelected(FramePresetNameTextBox.Text.Trim(), out _)) return;
        FrameTemplatePresetsListBox.Items.Refresh();
        UpdatePresetControls();
    }

    private void DuplicateFramePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_presetManager.SelectedTemplatePreset is null) return;
        _presetManager.DuplicateSelected();
        FrameTemplatePresetsListBox.SelectedItem = _presetManager.SelectedTemplatePreset;
        UpdatePresetControls();
    }

    private void DeleteFramePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_presetManager.DeleteSelectedPreset()) return;
        FrameTemplatePresetsListBox.SelectedItem = null;
        FramePresetNameTextBox.Clear();
        UpdatePresetControls();
    }

    private void FramePresetNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingPresetState) UpdatePresetControls();
    }

    private void RefreshAvailableNameTokens()
    {
        if (FrameAvailableTokensListBox is null || FrameUserDefinedTokensListBox is null) return;

        _availableNameTokens.Clear();
        AddStandardToken("PartNumber", "PartNumber");
        AddStandardToken("StockNumber", "StockNumber");
        AddStandardToken("Material", "Material");
        AddStandardToken("Description", "Description");
        _availableNameTokens.Add(new FrameNameTokenDefinition(
            "Length", _localization.GetString("Frame_ColumnLength")));
        AddStandardToken("Qty", "Quantity");
        AddStandardToken("FileName", "FileName");

        _userDefinedNameTokens.Clear();
        foreach (var property in PropertyMetadataRegistry.UserDefinedProperties.OrderBy(item => item.DisplayName))
            _userDefinedNameTokens.Add(new FrameNameTokenDefinition(property.TokenName, property.DisplayName, true));

        RenderFileNameTemplate();
    }

    private void AddStandardToken(string tokenName, string propertyName)
    {
        var displayName = PropertyMetadataRegistry.GetPropertyByInternalName(propertyName)?.DisplayName ?? tokenName;
        _availableNameTokens.Add(new FrameNameTokenDefinition(tokenName, displayName));
    }

    private void AddFileNameToken(string tokenName)
    {
        if (string.IsNullOrWhiteSpace(tokenName)) return;
        SetFileNameTemplate($"{_fileNameTemplate}{{{tokenName}}}");
    }

    private void AddCustomText(string customText)
    {
        if (string.IsNullOrEmpty(customText)) return;

        var trailingCustomText = TrailingCustomTextRegex.Match(_fileNameTemplate);
        if (trailingCustomText.Success)
        {
            var combined = trailingCustomText.Groups["custom"].Value + customText;
            SetFileNameTemplate(string.Concat(
                _fileNameTemplate.AsSpan(0, trailingCustomText.Index),
                $"{{CUSTOM:{combined}}}"));
        }
        else
        {
            SetFileNameTemplate($"{_fileNameTemplate}{{CUSTOM:{customText}}}");
        }
    }

    private void SetFileNameTemplate(string template)
    {
        _fileNameTemplate = template ?? "";
        RenderFileNameTemplate();
        UpdatePreview();
        UpdatePresetControls();
    }

    private void RenderFileNameTemplate()
    {
        if (FrameTokenContainer is null) return;

        FrameTokenContainer.Children.Clear();
        var cursor = 0;
        foreach (Match match in TemplateSegmentRegex.Matches(_fileNameTemplate))
        {
            if (match.Index > cursor)
            {
                var literal = _fileNameTemplate[cursor..match.Index];
                AddTemplateSegment(cursor, literal.Length, literal, true, false);
            }

            if (match.Groups["custom"].Success)
            {
                AddTemplateSegment(match.Index, match.Length, match.Groups["custom"].Value, true, false);
            }
            else
            {
                var tokenName = match.Groups["token"].Value;
                var token = FindNameToken(tokenName);
                AddTemplateSegment(match.Index, match.Length, token?.DisplayName ?? tokenName, false, token?.IsUserDefined == true);
            }
            cursor = match.Index + match.Length;
        }

        if (cursor < _fileNameTemplate.Length)
        {
            var literal = _fileNameTemplate[cursor..];
            AddTemplateSegment(cursor, literal.Length, literal, true, false);
        }
    }

    private void AddTemplateSegment(int startIndex, int length, string displayText, bool isCustom, bool isUserDefined)
    {
        if (FrameTokenContainer is null || length == 0) return;

        var border = new WpfBorder
        {
            Style = FrameTokenContainer.FindResource("TokenBlockStyle") as WpfStyle,
            Tag = isCustom ? "CustomText" : isUserDefined ? "UserDefined" : null,
            ToolTip = _fileNameTemplate.Substring(startIndex, length)
        };
        border.Child = new TextBlock
        {
            Text = displayText,
            Style = FrameTokenContainer.FindResource("TokenTextStyle") as WpfStyle
        };
        border.MouseDown += (_, args) =>
        {
            if (args.ClickCount != 2 || startIndex + length > _fileNameTemplate.Length) return;
            SetFileNameTemplate(_fileNameTemplate.Remove(startIndex, length));
        };
        FrameTokenContainer.Children.Add(border);
    }

    private FrameNameTokenDefinition? FindNameToken(string tokenName) =>
        _availableNameTokens.Concat(_userDefinedNameTokens)
            .FirstOrDefault(item => string.Equals(item.TokenName, tokenName, StringComparison.OrdinalIgnoreCase));

    private void UpdatePresetControls()
    {
        if (FrameEmptyStatePanel is null || FrameTemplatePresetsListBox is null) return;

        var isEmpty = _presetManager.TemplatePresets.Count == 0;
        FrameEmptyStatePanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        FrameTemplatePresetsListBox.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;

        var selected = _presetManager.SelectedTemplatePreset;
        var name = FramePresetNameTextBox?.Text.Trim() ?? "";
        var duplicateName = _presetManager.TemplatePresets.Any(
            preset => !ReferenceEquals(preset, selected) &&
                      string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));

        if (CreateFramePresetButton is not null)
            CreateFramePresetButton.IsEnabled = !string.IsNullOrWhiteSpace(name) &&
                                                !_presetManager.PresetNameExists(name) &&
                                                FrameFileNameService.ValidateTemplate(_fileNameTemplate);
        if (SaveFramePresetButton is not null)
            SaveFramePresetButton.IsEnabled = selected is not null && selected.Template != _fileNameTemplate;
        if (RenameFramePresetButton is not null)
            RenameFramePresetButton.IsEnabled = selected is not null &&
                                                !string.IsNullOrWhiteSpace(name) &&
                                                !duplicateName &&
                                                !string.Equals(selected.Name, name, StringComparison.Ordinal);
        if (DuplicateFramePresetButton is not null) DuplicateFramePresetButton.IsEnabled = selected is not null;
        if (DeleteFramePresetButton is not null) DeleteFramePresetButton.IsEnabled = selected is not null;
    }

    private void ExportFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFormatControls();
        UpdatePreview();
    }

    private void FrameMembersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (NamePreviewTextBlock is null) return;
        if (FrameEnableFileNameConstructorCheckBox?.IsChecked != true)
        {
            NamePreviewTextBlock.Text = _localization.GetString("Text_FunctionDisabled");
            NamePreviewTextBlock.Foreground = FindResource("TextMutedBrush") as WpfBrush;
            SetPreviewAppearance(false);
            return;
        }

        if (!FrameFileNameService.ValidateTemplate(_fileNameTemplate))
        {
            NamePreviewTextBlock.Text = _localization.GetString("Error_UnknownTokens");
            NamePreviewTextBlock.Foreground = FindResource("ErrorBrush") as WpfBrush;
            SetPreviewAppearance(false);
            return;
        }

        var member = FrameMembersGrid?.SelectedItem as FrameMemberData ?? _members.FirstOrDefault();
        NamePreviewTextBlock.Text = member is null
            ? CreatePlaceholderPreview() + GetSelectedExtension()
            : FrameFileNameService.Resolve(_fileNameTemplate, member) + GetSelectedExtension();
        NamePreviewTextBlock.Foreground = FindResource("TextSecondaryBrush") as WpfBrush;
        SetPreviewAppearance(true);
    }

    private void SetPreviewAppearance(bool isValid)
    {
        if (FrameFileNamePreviewBorder is null) return;
        FrameFileNamePreviewBorder.Background = FindResource(
            isValid ? "PreviewValidBackgroundBrush" : "PreviewWarningBackgroundBrush") as WpfBrush;
        FrameFileNamePreviewBorder.BorderBrush = FindResource(
            isValid ? "PreviewValidBorderBrush" : "PreviewWarningBorderBrush") as WpfBrush;
    }

    private string CreatePlaceholderPreview() => TemplateSegmentRegex.Replace(_fileNameTemplate, match =>
    {
        if (match.Groups["custom"].Success) return match.Groups["custom"].Value;
        var tokenName = match.Groups["token"].Value;
        return FindNameToken(tokenName)?.DisplayName ?? tokenName;
    });

    private void UpdateFormatControls()
    {
        if (IgesOptionsPanel is null || ExportFormatComboBox is null) return;
        IgesOptionsPanel.IsEnabled = ExportFormatComboBox.SelectedIndex == (int)FrameExportFormat.Iges;
    }

    private void UpdateExportPlacementControls()
    {
        if (FrameEnableSubfolderCheckBox is null || FrameSubfolderNameTextBox is null || FrameSelectFixedFolderButton is null)
            return;
        var usePartFolder = FramePartFolderRadioButton?.IsChecked == true;
        FrameEnableSubfolderCheckBox.IsEnabled = !usePartFolder;
        if (usePartFolder) FrameEnableSubfolderCheckBox.IsChecked = false;
        FrameSubfolderNameTextBox.IsEnabled = !usePartFolder && FrameEnableSubfolderCheckBox.IsChecked == true;
        FrameSelectFixedFolderButton.IsEnabled = FrameFixedFolderRadioButton?.IsChecked == true;
        OutputFolderTextBox.IsEnabled = FrameFixedFolderRadioButton?.IsChecked == true;
    }

    private void UpdateBomFormatControls()
    {
        if (FrameCsvDelimiterComboBox is not null)
            FrameCsvDelimiterComboBox.IsEnabled = FrameCsvFormatRadioButton?.IsChecked == true;
    }

    private void SetSelectedExportFolder(ExportFolderType folderType)
    {
        switch (folderType)
        {
            case ExportFolderType.ComponentFolder: FrameComponentFolderRadioButton.IsChecked = true; break;
            case ExportFolderType.PartFolder: FramePartFolderRadioButton.IsChecked = true; break;
            case ExportFolderType.ProjectFolder: FrameProjectFolderRadioButton.IsChecked = true; break;
            case ExportFolderType.FixedFolder: FrameFixedFolderRadioButton.IsChecked = true; break;
            default: FrameChooseFolderRadioButton.IsChecked = true; break;
        }
    }

    private ExportFolderType GetSelectedExportFolder()
    {
        if (FrameComponentFolderRadioButton.IsChecked == true) return ExportFolderType.ComponentFolder;
        if (FramePartFolderRadioButton.IsChecked == true) return ExportFolderType.PartFolder;
        if (FrameProjectFolderRadioButton.IsChecked == true) return ExportFolderType.ProjectFolder;
        if (FrameFixedFolderRadioButton.IsChecked == true) return ExportFolderType.FixedFolder;
        return ExportFolderType.ChooseFolder;
    }

    private bool TryResolveExportFolder(out string outputFolder, out bool usePartFolder)
    {
        outputFolder = "";
        usePartFolder = false;
        var activeDocument = _inventorManager?.Application?.ActiveDocument;
        var assemblyFolder = activeDocument is null || string.IsNullOrWhiteSpace(activeDocument.FullFileName)
            ? ""
            : IOPath.GetDirectoryName(activeDocument.FullFileName) ?? "";

        switch (GetSelectedExportFolder())
        {
            case ExportFolderType.ChooseFolder:
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = _localization.GetString("Frame_SelectOutputFolder"),
                    SelectedPath = Directory.Exists(assemblyFolder) ? assemblyFolder : ""
                })
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
                    outputFolder = dialog.SelectedPath;
                }
                break;
            case ExportFolderType.ComponentFolder:
                outputFolder = assemblyFolder;
                break;
            case ExportFolderType.PartFolder:
                outputFolder = assemblyFolder;
                usePartFolder = true;
                break;
            case ExportFolderType.ProjectFolder:
                _inventorManager?.SetProjectFolderInfo();
                outputFolder = _inventorManager?.ProjectWorkspacePath ?? "";
                break;
            case ExportFolderType.FixedFolder:
                outputFolder = OutputFolderTextBox.Text.Trim();
                break;
        }

        if (usePartFolder && _members.Any(member => !string.IsNullOrWhiteSpace(IOPath.GetDirectoryName(member.FullFileName))))
            return true;
        if (!string.IsNullOrWhiteSpace(outputFolder)) return true;
        ShowError(_localization.GetString("Frame_ErrorOutputFolder"));
        return false;
    }

    private string GetBomExportFileName()
    {
        var fallback = $"Export_{DateTime.Now:yyyyMMdd_HHmmss}";
        var type = FrameBomFileNameComboBox.SelectedItem is ExcelExportFileNameType selected
            ? selected
            : ExcelExportFileNameType.DateTimeFormat;
        var document = _inventorManager?.Application?.ActiveDocument;
        if (document is null || type == ExcelExportFileNameType.DateTimeFormat) return fallback;

        string value;
        if (type == ExcelExportFileNameType.FileName)
        {
            value = IOPath.GetFileNameWithoutExtension(document.DisplayName);
        }
        else
        {
            value = new PropertyManager(document).GetMappedProperty("PartNumber");
        }

        var invalid = IOPath.GetInvalidFileNameChars();
        value = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private IReadOnlyList<FrameBomColumn> GetSelectedBomColumns() => FrameMembersGrid.Columns
        .OrderBy(column => column.DisplayIndex)
        .Select(CreateBomColumn)
        .Where(column => column is not null)
        .Cast<FrameBomColumn>()
        .ToList();

    private static FrameBomColumn? CreateBomColumn(DataGridColumn column)
    {
        var header = column.Header?.ToString() ?? "";
        var attributeName = GetAttributeInternalName(column);
        if (attributeName is not null)
            return new FrameBomColumn(header, member => member.AttributeValues.GetValueOrDefault(attributeName, ""));

        return column.SortMemberPath switch
        {
            "ProcessingStatus" => new FrameBomColumn(header, member => member.ProcessingStatusText),
            "LengthMm" => new FrameBomColumn(header, member => member.LengthMm),
            "Quantity" => new FrameBomColumn(header, member => member.Quantity),
            "FileName" => new FrameBomColumn(header, member => member.FileName),
            "OutputFile" => new FrameBomColumn(header, member => member.OutputFile),
            _ => null
        };
    }

    private string GetSelectedExtension() => (FrameExportFormat)Math.Max(0, ExportFormatComboBox?.SelectedIndex ?? 0) switch
    {
        FrameExportFormat.Iges => ".igs",
        FrameExportFormat.Step => ".stp",
        FrameExportFormat.Sat => ".sat",
        FrameExportFormat.Stl => ".stl",
        _ => ".igs"
    };

    private void SetDefaultOutputFolder(AssemblyDocument assemblyDocument)
    {
        if (!string.IsNullOrWhiteSpace(OutputFolderTextBox.Text)) return;
        var assemblyPath = assemblyDocument.FullFileName;
        var parentFolder = string.IsNullOrWhiteSpace(assemblyPath) ? null : IOPath.GetDirectoryName(assemblyPath);
        OutputFolderTextBox.Text = IOPath.Combine(parentFolder ?? IOPath.GetTempPath(), "3D");
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        ScanFrameButton.IsEnabled = !_isBusy;
        Export3dButton.IsEnabled = !_isBusy && _members.Count > 0;
        ExportBomButton.IsEnabled = !_isBusy && _members.Count > 0;
        ClearFrameButton.IsEnabled = !_isBusy;
        CancelFrameButton.Visibility = _isBusy ? Visibility.Visible : Visibility.Collapsed;
        CancelFrameButton.IsEnabled = _isBusy && _operationCancellation?.IsCancellationRequested != true;
    }

    private void ShowError(string message)
    {
        StatusTextBlock.Text = message;
        CustomMessageBox.Show(message, _localization.GetString("MessageBox_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ShowErrors(IReadOnlyList<string> errors)
    {
        var visibleErrors = errors.Take(10).ToList();
        var message = string.Join(System.Environment.NewLine, visibleErrors);
        if (errors.Count > visibleErrors.Count)
            message += System.Environment.NewLine + $"… +{errors.Count - visibleErrors.Count}";
        ShowError(message);
    }

    private static int ClampIndex(int value, int count, int fallback) => value >= 0 && value < count ? value : fallback;
}

public sealed record FrameNameTokenDefinition(string TokenName, string DisplayName, bool IsUserDefined = false);
