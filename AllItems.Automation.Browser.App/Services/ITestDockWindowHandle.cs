using System.Windows;

namespace AllItems.Automation.Browser.App.Services;

public interface ITestDockWindowHandle
{
    event EventHandler? Closed;

    WindowState WindowState { get; set; }

    void Show();

    void Activate();

    void Focus();
}
