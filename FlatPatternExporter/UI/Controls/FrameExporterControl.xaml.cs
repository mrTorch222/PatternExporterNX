using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FlatPatternExporter.Core;
using FlatPatternExporter.Models;
using FlatPatternExporter.Services;
using FlatPatternExporter.UI.Windows;
using Inventor;
using IOPath = System.IO.Path;

namespace FlatPatternExporter.UI.Controls;

public partial class FrameExporterControl : System.Windows.Controls.UserControl
{
    private readonly ObservableCollection<FrameMemberData> _members = [];
    private readonly Dictionary<string, PartDocument> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly LocalizationManager _localization = LocalizationManager.Instance;
    private InventorManager? _inventorManager;
    private bool _isBusy;

    public FrameExporterControl()
    {
        InitializeComponent();
        FrameMembersGrid.ItemsSource = _members;
        NameTemplateTextBox.Text = FrameFileNameService.DefaultTemplate;
        UpdatePreview();
    }

    public void Initialize(InventorManager inventorManager)
    {
        _inventorManager = inventorManager;
    }

    public void ApplySettings(FrameExportSettings? settings)
    {
        settings ??= new FrameExportSettings();
        OutputFolderTextBox.Text = settings.OutputFolder;
        NameTemplateTextBox.Text = string.IsNullOrWhiteSpace(settings.FileNameTemplate)
            ? FrameFileNameService.DefaultTemplate
            : settings.FileNameTemplate;
        GeometryTypeComboBox.SelectedIndex = ClampIndex(settings.GeometryType, GeometryTypeComboBox.Items.Count, 0);
        SolidFaceTypeComboBox.SelectedIndex = ClampIndex(settings.SolidFaceType, SolidFaceTypeComboBox.Items.Count, 1);
        SurfaceTypeComboBox.SelectedIndex = ClampIndex(settings.SurfaceType, SurfaceTypeComboBox.Items.Count, 1);
        UpdatePreview();
    }

    public FrameExportSettings CollectSettings() => new()
    {
        OutputFolder = OutputFolderTextBox.Text.Trim(),
        FileNameTemplate = NameTemplateTextBox.Text,
        GeometryType = GeometryTypeComboBox.SelectedIndex,
        SolidFaceType = SolidFaceTypeComboBox.SelectedIndex,
        SurfaceType = SurfaceTypeComboBox.SelectedIndex
    };

    private async void ScanFrameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _inventorManager is null) return;

        try
        {
            SetBusy(true);
            StatusTextBlock.Text = _localization.GetString("Frame_StatusScanning");

            var validation = _inventorManager.ValidateActiveDocument();
            if (!validation.IsValid || validation.Document is not AssemblyDocument assemblyDocument)
            {
                ShowError(_localization.GetString("Frame_ErrorAssemblyRequired"));
                return;
            }

            var result = await Task.Run(() => new FrameMemberScanner().Scan(assemblyDocument));
            _members.Clear();
            _documents.Clear();
            foreach (var member in result.Members) _members.Add(member);
            foreach (var pair in result.Documents) _documents.Add(pair.Key, pair.Value);

            SetDefaultOutputFolder(assemblyDocument);
            StatusTextBlock.Text = _localization.GetString("Frame_StatusScanComplete", _members.Count, result.Errors.Count);
            if (_members.Count == 0) ShowError(_localization.GetString("Frame_InfoNoMembers"));
            else if (result.Errors.Count > 0) ShowError(result.Errors[0]);
            UpdatePreview();
        }
        catch (Exception ex)
        {
            ShowError(_localization.GetString("Frame_Error", ex.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ExportIgesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy || _inventorManager is null || _members.Count == 0) return;

        var outputFolder = OutputFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            ShowError(_localization.GetString("Frame_ErrorOutputFolder"));
            return;
        }

        try
        {
            SetBusy(true);
            StatusTextBlock.Text = _localization.GetString("Frame_StatusExporting");
            var options = new FrameExportOptions(
                outputFolder,
                NameTemplateTextBox.Text,
                GeometryTypeComboBox.SelectedIndex,
                SolidFaceTypeComboBox.SelectedIndex,
                SurfaceTypeComboBox.SelectedIndex);
            var exporter = new FrameIgesExporter(_inventorManager);
            var result = await Task.Run(() => exporter.Export(_members, _documents, options));
            StatusTextBlock.Text = _localization.GetString("Frame_StatusExportComplete", result.ExportedCount, result.Errors.Count);
            if (result.Errors.Count > 0) ShowError(result.Errors[0]);
        }
        catch (Exception ex)
        {
            ShowError(_localization.GetString("Frame_Error", ex.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ExportBomButton_Click(object sender, RoutedEventArgs e)
    {
        if (_members.Count == 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = _localization.GetString("Frame_ButtonExportBom"),
            Filter = _localization.GetString("Frame_BomFileFilter"),
            DefaultExt = ".xlsx",
            FileName = "Frame_BOM.xlsx",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            if (string.Equals(IOPath.GetExtension(dialog.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
                FrameBomExportService.ExportCsv(dialog.FileName, _members);
            else
                FrameBomExportService.ExportExcel(dialog.FileName, _members);
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

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _members.Clear();
        _documents.Clear();
        StatusTextBlock.Text = "";
        UpdateButtons();
        UpdatePreview();
    }

    private void NameTemplateTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void FrameMembersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (NamePreviewTextBlock is null || NameTemplateTextBox is null) return;
        var member = FrameMembersGrid?.SelectedItem as FrameMemberData ?? _members.FirstOrDefault();
        NamePreviewTextBlock.Text = member is null ? "" : FrameFileNameService.Resolve(NameTemplateTextBox.Text, member) + ".igs";
    }

    private void SetDefaultOutputFolder(AssemblyDocument assemblyDocument)
    {
        if (!string.IsNullOrWhiteSpace(OutputFolderTextBox.Text)) return;
        var assemblyPath = assemblyDocument.FullFileName;
        var parentFolder = string.IsNullOrWhiteSpace(assemblyPath) ? null : IOPath.GetDirectoryName(assemblyPath);
        OutputFolderTextBox.Text = IOPath.Combine(parentFolder ?? IOPath.GetTempPath(), "IGES");
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        ScanFrameButton.IsEnabled = !_isBusy;
        ExportIgesButton.IsEnabled = !_isBusy && _members.Count > 0;
        ExportBomButton.IsEnabled = !_isBusy && _members.Count > 0;
    }

    private void ShowError(string message)
    {
        StatusTextBlock.Text = message;
        CustomMessageBox.Show(message, _localization.GetString("MessageBox_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static int ClampIndex(int value, int count, int fallback) => value >= 0 && value < count ? value : fallback;
}
