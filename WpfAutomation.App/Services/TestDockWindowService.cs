using System.Windows;

namespace WpfAutomation.App.Services;

public sealed class TestDockWindowService : ITestDockWindowService
{
    private readonly ITestDockWindowFactory _windowFactory;
    private ITestDockWindowHandle? _window;

    public TestDockWindowService(ITestDockWindowFactory windowFactory)
    {
        _windowFactory = windowFactory;
    }

    public void Show()
    {
        if (_window is not null)
        {
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }

            _window.Activate();
            _window.Focus();
            return;
        }

        _window = _windowFactory.Create();
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }
}
