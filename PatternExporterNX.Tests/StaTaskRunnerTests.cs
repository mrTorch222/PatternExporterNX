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

    [Fact]
    public async Task CancellationBeforeStartReturnsCancelledTask()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StaTaskRunner.RunAsync(() => 1, cancellation.Token));
    }

    [Fact]
    public async Task CompletedActionKeepsItsResultWhenCancellationArrivesDuringExecution()
    {
        using var cancellation = new CancellationTokenSource();

        var result = await StaTaskRunner.RunAsync(() =>
        {
            cancellation.Cancel();
            return 42;
        }, cancellation.Token);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ConsecutiveActionsUseTheSameStaThread()
    {
        var firstThread = await StaTaskRunner.RunAsync(() => Environment.CurrentManagedThreadId);
        var secondThread = await StaTaskRunner.RunAsync(() => Environment.CurrentManagedThreadId);

        Assert.Equal(firstThread, secondThread);
    }
}
