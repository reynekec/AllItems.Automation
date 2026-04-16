namespace AllItems.Automation.Browser.Core.Diagnostics;

public sealed class CancellationManager : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public CancellationToken Token => _cancellationTokenSource.Token;

    public void Cancel()
    {
        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }
}