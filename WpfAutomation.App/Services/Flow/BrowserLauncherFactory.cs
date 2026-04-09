using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.App.Services.Flow;

public interface IBrowserLauncherFactory
{
    IBrowserLauncher Create(BrowserType browserType);
}

public sealed class BrowserLauncherFactory : IBrowserLauncherFactory
{
    private readonly DiagnosticsService _diagnosticsService;

    public BrowserLauncherFactory(DiagnosticsService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
    }

    public IBrowserLauncher Create(BrowserType browserType)
    {
        return new BrowserLauncher(browserType, diagnosticsService: _diagnosticsService);
    }
}
