using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using FlatPatternExporter.Core;
using FlatPatternExporter.Features.Acceptance.Models;
using FlatPatternExporter.Features.Acceptance.Services;
using FlatPatternExporter.Services;
using Point = System.Windows.Point;

namespace FlatPatternExporter.Features.Acceptance.UI;

public partial class AcceptanceSheetWindow : Window, INotifyPropertyChanged
{
    private readonly AcceptancePreviewService _previewService;
    private readonly LocalizationManager _localization = LocalizationManager.Instance;
    private AcceptanceSheetItem? _selectedItem;
    private bool _isRendering;
    private string _status = "";
    private Point? _dragStart;
    private double _startYaw;
    private double _startPitch;

    public ObservableCollection<AcceptanceSheetItem> Items { get; }
    public AcceptanceSheetItem? SelectedItem { get => _selectedItem; private set { _selectedItem = value; OnPropertyChanged(); } }
    public bool IsRendering { get => _isRendering; private set { _isRendering = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanExport)); } }
    public bool CanExport => Items.Count > 0 && !IsRendering;
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }

    public AcceptanceSheetWindow(IEnumerable<AcceptanceSheetItem> items, InventorManager inventorManager)
    {
        Items = new ObservableCollection<AcceptanceSheetItem>(items);
        _previewService = new AcceptancePreviewService(inventorManager);
        InitializeComponent();
        DataContext = this;
    }

    private async void PartsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SelectedItem = PartsList.SelectedItem as AcceptanceSheetItem;
        if (SelectedItem?.Preview is null) await RenderSelectedAsync();
    }

    private async Task RenderSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await RenderItemAsync(item);
    }

    private async Task RenderItemAsync(AcceptanceSheetItem item)
    {
        if (IsRendering) return;
        try
        {
            IsRendering = true;
            Status = _localization.GetString("Acceptance_RenderingPart", item.PartNumber);
            item.Preview = await _previewService.RenderAsync(item);
            item.HasTransparentPreview = true;
            Status = _localization.GetString("Acceptance_Ready");
        }
        catch (Exception exception)
        {
            Status = _localization.GetString("Acceptance_RenderFailed", exception.Message);
        }
        finally { IsRendering = false; }
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SelectedItem is null) return;
        _dragStart = e.GetPosition(PreviewSurface);
        _startYaw = SelectedItem.Yaw;
        _startPitch = SelectedItem.Pitch;
        PreviewSurface.CaptureMouse();
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null || SelectedItem is null || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(PreviewSurface);
        SelectedItem.Yaw = _startYaw + (point.X - _dragStart.Value.X) * 0.45;
        SelectedItem.Pitch = _startPitch - (point.Y - _dragStart.Value.Y) * 0.45;
    }

    private async void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null) return;
        _dragStart = null;
        PreviewSurface.ReleaseMouseCapture();
        await RenderSelectedAsync();
    }

    private async void ResetView_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is null) return;
        SelectedItem.Yaw = 0;
        SelectedItem.Pitch = 0;
        await RenderSelectedAsync();
    }

    private async void CameraOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        await RenderSelectedAsync();
    }

    private async void AngleSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => await RenderSelectedAsync();

    private async void RefreshAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
        {
            await RenderItemAsync(item);
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items.Where(item => !item.HasTransparentPreview))
            await RenderItemAsync(item);
        if (Items.Any(item => !item.HasTransparentPreview))
        {
            Status = _localization.GetString("Acceptance_RenderRequired");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = _localization.GetString("Acceptance_Export"),
            Filter = "Excel (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = $"Acceptance_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            AcceptanceSheetExcelExporter.Export(dialog.FileName, Items);
            Status = _localization.GetString("Acceptance_Saved", dialog.FileName);
        }
        catch (Exception exception)
        {
            Status = _localization.GetString("Acceptance_ExportFailed", exception.Message);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
