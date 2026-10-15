using System.Collections.Concurrent;
using FlatPatternExporter.Features.Acceptance.Models;

namespace FlatPatternExporter.Features.Acceptance.Services;

public static class AcceptanceViewStateStore
{
    private sealed record ViewState(
        AcceptanceViewPreset Preset,
        double Yaw,
        double Pitch,
        bool Perspective,
        double PerspectiveAngle);

    private static readonly ConcurrentDictionary<string, ViewState> States = new(StringComparer.OrdinalIgnoreCase);

    public static void Save(IEnumerable<AcceptanceSheetItem> items)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.SourceKey)) continue;
            States[item.SourceKey] = new ViewState(
                item.ViewPreset, item.Yaw, item.Pitch, item.Perspective, item.PerspectiveAngle);
        }
    }

    public static void Restore(IEnumerable<AcceptanceSheetItem> items)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.SourceKey) || !States.TryGetValue(item.SourceKey, out var state)) continue;
            item.ViewPreset = state.Preset;
            item.Yaw = state.Yaw;
            item.Pitch = state.Pitch;
            item.Perspective = state.Perspective;
            item.PerspectiveAngle = state.PerspectiveAngle;
            item.HasTransparentPreview = false;
        }
    }
}
