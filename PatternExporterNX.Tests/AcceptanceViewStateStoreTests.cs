using FlatPatternExporter.Features.Acceptance.Models;
using FlatPatternExporter.Features.Acceptance.Services;

namespace PatternExporterNX.Tests;

public sealed class AcceptanceViewStateStoreTests
{
    [Fact]
    public void SaveAndRestore_PreservesIndependentCameraStateForSourceKey()
    {
        var key = $"part-{Guid.NewGuid():N}";
        var source = new AcceptanceSheetItem
        {
            SourceKey = key,
            ViewPreset = AcceptanceViewPreset.Top,
            Yaw = 24.5,
            Pitch = -18,
            Perspective = false,
            PerspectiveAngle = 42
        };
        AcceptanceViewStateStore.Save([source]);

        var restored = new AcceptanceSheetItem { SourceKey = key, HasTransparentPreview = true };
        AcceptanceViewStateStore.Restore([restored]);

        Assert.Equal(AcceptanceViewPreset.Top, restored.ViewPreset);
        Assert.Equal(24.5, restored.Yaw);
        Assert.Equal(-18, restored.Pitch);
        Assert.False(restored.Perspective);
        Assert.Equal(42, restored.PerspectiveAngle);
        Assert.False(restored.HasTransparentPreview);
    }
}
