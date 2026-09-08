using System.ComponentModel;
using System.Runtime.CompilerServices;
using FlatPatternExporter.Enums;
using FlatPatternExporter.Services;

namespace FlatPatternExporter.Features.Frame.Models;

public sealed class FrameMemberData : INotifyPropertyChanged
{
    private ProcessingStatus _processingStatus = ProcessingStatus.NotProcessed;
    private string _outputFile = "";

    public FrameMemberData()
    {
        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    public string DocumentKey { get; init; } = "";
    public string FileName { get; init; } = "";
    public string FullFileName { get; init; } = "";
    public string PartNumber { get; init; } = "";
    public string StockNumber { get; init; } = "";
    public string Material { get; init; } = "";
    public string Description { get; init; } = "";
    public double? LengthMm { get; init; }
    public int Quantity { get; set; }

    public ProcessingStatus ProcessingStatus
    {
        get => _processingStatus;
        set
        {
            if (_processingStatus == value) return;
            _processingStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProcessingStatusText));
        }
    }

    public string ProcessingStatusText => ProcessingStatus switch
    {
        ProcessingStatus.NotProcessed => LocalizationManager.Instance.GetString("ProcessingStatus_NotProcessed"),
        ProcessingStatus.Pending => LocalizationManager.Instance.GetString("ProcessingStatus_Pending"),
        ProcessingStatus.Success => LocalizationManager.Instance.GetString("ProcessingStatus_Success"),
        ProcessingStatus.Skipped => LocalizationManager.Instance.GetString("ProcessingStatus_Skipped"),
        ProcessingStatus.Interrupted => LocalizationManager.Instance.GetString("ProcessingStatus_Interrupted"),
        _ => ProcessingStatus.ToString()
    };

    public string OutputFile
    {
        get => _outputFile;
        set
        {
            if (_outputFile == value) return;
            _outputFile = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(ProcessingStatusText));

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
