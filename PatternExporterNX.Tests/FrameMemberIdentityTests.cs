using FlatPatternExporter.Features.Frame.Services;

namespace PatternExporterNX.Tests;

public sealed class FrameMemberIdentityTests
{
    [Fact]
    public void DifferentModelStatesProduceDifferentKeys()
    {
        var primary = FrameMemberIdentity.Create(@"C:\Models\Tube.ipt", "Tube.ipt", "Primary");
        var shortMember = FrameMemberIdentity.Create(@"C:\Models\Tube.ipt", "Tube.ipt", "Short");

        Assert.NotEqual(primary, shortMember);
    }

    [Fact]
    public void UnsavedDocumentsUseDisplayName()
    {
        Assert.Equal("display:Tube.ipt|model-state:Primary", FrameMemberIdentity.Create("", " Tube.ipt ", "Primary"));
    }
}
