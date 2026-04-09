using System.Windows;

namespace WpfAutomation.App.Services;

public interface ITestDockWindowHandle
{
    event EventHandler? Closed;

    WindowState WindowState { get; set; }

    void Show();

    void Activate();

    void Focus();
}
