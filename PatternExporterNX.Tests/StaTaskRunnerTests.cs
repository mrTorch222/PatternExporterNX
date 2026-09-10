using FlatPatternExporter.Core;

namespace PatternExporterNX.Tests;

public sealed class StaTaskRunnerTests
{
    [Fact]
    public async Task ActionRunsInStaApartment()
    {
        var apartment = await StaTaskRunner.RunAsync(() => Thread.CurrentThread.GetApartmentState());

        Assert.Equal(ApartmentState.STA, apartment);
    }
}
