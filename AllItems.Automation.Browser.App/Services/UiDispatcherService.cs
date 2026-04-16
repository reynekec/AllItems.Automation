using System.Windows;
using System.Windows.Threading;

namespace AllItems.Automation.Browser.App.Services;

public sealed class UiDispatcherService : IUiDispatcherService
{
    private readonly Dispatcher _dispatcher;

    public UiDispatcherService()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public async Task InvokeAsync(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await _dispatcher.InvokeAsync(action);
    }
}
