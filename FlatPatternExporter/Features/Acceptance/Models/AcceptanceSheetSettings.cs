using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlatPatternExporter.Features.Acceptance.Models;

public enum AcceptancePaperSize { A4, A3 }

public sealed class AcceptanceSheetSettings : INotifyPropertyChanged
{
    private AcceptancePaperSize _paperSize = AcceptancePaperSize.A4;
    private bool _landscape;
    private int _cardsPerRow = 2;
    private bool _showMaterial = true;
    private bool _showDetails = true;

    public AcceptancePaperSize PaperSize { get => _paperSize; set => Set(ref _paperSize, value); }
    public bool Landscape { get => _landscape; set => Set(ref _landscape, value); }
    public int CardsPerRow { get => _cardsPerRow; set => Set(ref _cardsPerRow, Math.Clamp(value, 1, 3)); }
    public bool ShowMaterial { get => _showMaterial; set => Set(ref _showMaterial, value); }
    public bool ShowDetails { get => _showDetails; set => Set(ref _showDetails, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
