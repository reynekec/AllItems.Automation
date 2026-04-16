using System.Windows.Input;

namespace AllItems.Automation.Browser.App.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private CancellationTokenSource? _inFlightCts;

    public AsyncRelayCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _inFlightCts?.Dispose();
        _inFlightCts = new CancellationTokenSource();

        RaiseCanExecuteChanged();

        try
        {
            await _execute(_inFlightCts.Token);
        }
        finally
        {
            _inFlightCts.Dispose();
            _inFlightCts = null;
            RaiseCanExecuteChanged();
        }
    }

    public void Cancel()
    {
        _inFlightCts?.Cancel();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
