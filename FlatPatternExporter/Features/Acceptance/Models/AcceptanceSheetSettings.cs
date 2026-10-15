using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace FlatPatternExporter.Features.Acceptance.Models;

public enum AcceptancePaperSize { A4, A3 }
public enum AcceptanceImagePlacement { Left, Top }
public enum AcceptanceBorderWeight { Thin, Medium, Thick }
public enum AcceptanceCardField { PartNumber, Description, Material, Details }

public sealed class AcceptanceCardFieldOption : INotifyPropertyChanged
{
    private bool _isVisible = true;
    private string _label = "";
    private string _previewValue = "";

    public required AcceptanceCardField Field { get; init; }
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
    public string Label { get => _label; set => Set(ref _label, value); }
    public string PreviewValue { get => _previewValue; set => Set(ref _previewValue, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class AcceptanceSheetSettings : INotifyPropertyChanged
{
    private AcceptancePaperSize _paperSize = AcceptancePaperSize.A4;
    private bool _landscape;
    private int _cardsPerRow = 2;
    private bool _showMaterial = true;
    private bool _showDetails = true;
    private AcceptanceImagePlacement _imagePlacement = AcceptanceImagePlacement.Left;
    private AcceptanceBorderWeight _borderWeight = AcceptanceBorderWeight.Medium;
    private double _quantityFontSize = 30;
    private bool _showFieldLabels;

    public AcceptanceSheetSettings()
    {
        Fields =
        [
            new() { Field = AcceptanceCardField.PartNumber, Label = "Part number" },
            new() { Field = AcceptanceCardField.Description, Label = "Description" },
            new() { Field = AcceptanceCardField.Material, Label = "Material" },
            new() { Field = AcceptanceCardField.Details, Label = "Details" }
        ];
    }

    public AcceptancePaperSize PaperSize { get => _paperSize; set => Set(ref _paperSize, value); }
    public bool Landscape { get => _landscape; set => Set(ref _landscape, value); }
    public int CardsPerRow { get => _cardsPerRow; set => Set(ref _cardsPerRow, Math.Clamp(value, 1, 3)); }
    public bool ShowMaterial { get => _showMaterial; set => Set(ref _showMaterial, value); }
    public bool ShowDetails { get => _showDetails; set => Set(ref _showDetails, value); }
    public AcceptanceImagePlacement ImagePlacement { get => _imagePlacement; set => Set(ref _imagePlacement, value); }
    public AcceptanceBorderWeight BorderWeight { get => _borderWeight; set => Set(ref _borderWeight, value); }
    public double QuantityFontSize { get => _quantityFontSize; set => Set(ref _quantityFontSize, Math.Clamp(value, 18, 48)); }
    public bool ShowFieldLabels { get => _showFieldLabels; set => Set(ref _showFieldLabels, value); }
    public ObservableCollection<AcceptanceCardFieldOption> Fields { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
