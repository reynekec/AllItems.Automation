using System.Windows;

namespace AllItems.Automation.Browser.App.Services;

public sealed class TestDockWindowHandle : ITestDockWindowHandle
{
    private readonly Window _window;

    public TestDockWindowHandle(Window window)
    {
        _window = window;
        _window.Closed += OnWindowClosed;
    }

    public event EventHandler? Closed;

    public WindowState WindowState
    {
        get => _window.WindowState;
        set => _window.WindowState = value;
    }

    public void Show()
    {
        _window.Show();
    }

    public void Activate()
    {
        _window.Activate();
    }

    public void Focus()
    {
        _window.Focus();
    }

    private void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        Closed?.Invoke(this, eventArgs);
    }
}
