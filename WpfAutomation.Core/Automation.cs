using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Configuration;

namespace WpfAutomation.Core;

/// <summary>
/// Entry point for creating browser automation sessions.
/// </summary>
public static class Automation
{
    /// <summary>
    /// Creates a browser launcher for the specified browser type.
    /// </summary>
    /// <param name="type">The browser engine to launch.</param>
    /// <returns>A launcher that can start sessions or navigate directly.</returns>
    public static IBrowserLauncher OpenBrowser(BrowserType type)
    {
        return new BrowserLauncher(type);
    }
}