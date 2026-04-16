namespace AllItems.Automation.Browser.App.Services.Flow;

public interface IFlowRunExecutionControl : IDisposable
{
    bool IsPauseRequested { get; }

    void RequestPause();

    void Resume();

    Task WaitIfPausedAsync(CancellationToken cancellationToken = default);
}

public sealed class FlowRunExecutionControl : IFlowRunExecutionControl
{
    private readonly object _sync = new();
    private TaskCompletionSource<bool>? _resumeSignal;
    private bool _isPauseRequested;
    private bool _isDisposed;

    public bool IsPauseRequested
    {
        get
        {
            lock (_sync)
            {
                return _isPauseRequested;
            }
        }
    }

    public void RequestPause()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            _isPauseRequested = true;
            _resumeSignal ??= CreateSignal();
        }
    }

    public void Resume()
    {
        TaskCompletionSource<bool>? signal;

        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isPauseRequested = false;
            signal = _resumeSignal;
            _resumeSignal = null;
        }

        signal?.TrySetResult(true);
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken = default)
    {
        Task waitTask;

        lock (_sync)
        {
            ThrowIfDisposed();

            if (!_isPauseRequested)
            {
                return;
            }

            _resumeSignal ??= CreateSignal();
            waitTask = _resumeSignal.Task;
        }

        await waitTask.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        TaskCompletionSource<bool>? signal;

        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isPauseRequested = false;
            signal = _resumeSignal;
            _resumeSignal = null;
        }

        signal?.TrySetResult(true);
    }

    private static TaskCompletionSource<bool> CreateSignal()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(FlowRunExecutionControl));
        }
    }
}