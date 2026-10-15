using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace FlatPatternExporter.Features.Acceptance.Models;

public enum AcceptanceViewPreset
{
    IsoTopRight,
    IsoTopLeft,
    IsoBottomRight,
    IsoBottomLeft,
    Front,
    Back,
    Top,
    Bottom,
    Right,
    Left,
    Custom
}

public sealed class AcceptanceSheetItem : INotifyPropertyChanged
{
    private BitmapImage? _preview;
    private double _yaw;
    private double _pitch;
    private bool _perspective = true;
    private double _perspectiveAngle = 35;
    private AcceptanceViewPreset _viewPreset = AcceptanceViewPreset.IsoTopRight;

    public string SourceKey { get; init; } = "";
    public string FullFileName { get; init; } = "";
    public string ModelState { get; init; } = "";
    public string PartNumber { get; init; } = "";
    public string Description { get; init; } = "";
    public string Material { get; init; } = "";
    public string Details { get; init; } = "";
    public int Quantity { get; init; }
    public bool HasTransparentPreview { get; set; }
    public AcceptanceViewPreset ViewPreset
    {
        get => _viewPreset;
        set { if (_viewPreset == value) return; _viewPreset = value; HasTransparentPreview = false; OnPropertyChanged(); }
    }

    public BitmapImage? Preview
    {
        get => _preview;
        set { if (ReferenceEquals(_preview, value)) return; _preview = value; OnPropertyChanged(); }
    }

    public double Yaw
    {
        get => _yaw;
        set { if (Math.Abs(_yaw - value) < 0.01) return; _yaw = value; HasTransparentPreview = false; OnPropertyChanged(); }
    }

    public double Pitch
    {
        get => _pitch;
        set { var clamped = Math.Clamp(value, -85, 85); if (Math.Abs(_pitch - clamped) < 0.01) return; _pitch = clamped; HasTransparentPreview = false; OnPropertyChanged(); }
    }

    public bool Perspective
    {
        get => _perspective;
        set { if (_perspective == value) return; _perspective = value; HasTransparentPreview = false; OnPropertyChanged(); }
    }

    public double PerspectiveAngle
    {
        get => _perspectiveAngle;
        set { var clamped = Math.Clamp(value, 10, 80); if (Math.Abs(_perspectiveAngle - clamped) < 0.01) return; _perspectiveAngle = clamped; HasTransparentPreview = false; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
