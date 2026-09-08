using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlatPatternExporter.Models;

public sealed class AssemblyHierarchyNode : INotifyPropertyChanged
{
    private int _multiplier = 1;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public string FullFileName { get; init; } = "";
    public AssemblyHierarchyNode? Parent { get; init; }
    public ObservableCollection<AssemblyHierarchyNode> Children { get; } = [];
    public int DirectPartCount { get; set; }

    public int Multiplier
    {
        get => _multiplier;
        set
        {
            var normalized = Math.Max(1, value);
            if (_multiplier == normalized) return;
            _multiplier = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveMultiplier));
        }
    }

    public int EffectiveMultiplier => (int)Math.Min(
        int.MaxValue,
        (long)(Parent?.EffectiveMultiplier ?? 1) * Multiplier);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record PartOccurrenceContribution(
    string PartNumber,
    string AssemblyNodeId,
    string StructurePath,
    int BaseQuantity);

public sealed record HierarchyPartQuantity(
    string Key,
    string PartNumber,
    string StructurePath,
    int Quantity);
