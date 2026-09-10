namespace FlatPatternExporter.Core;

public static class StaTaskRunner
{
    private static readonly Lazy<System.Windows.Threading.Dispatcher> WorkerDispatcher = new(CreateWorkerDispatcher);

    public static Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        WorkerDispatcher.Value.BeginInvoke(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult(action());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });
        return completion.Task;
    }

    private static System.Windows.Threading.Dispatcher CreateWorkerDispatcher()
    {
        var ready = new TaskCompletionSource<System.Windows.Threading.Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.TrySetResult(dispatcher);
            System.Windows.Threading.Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "PatternExporterNX Inventor STA"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }
}
