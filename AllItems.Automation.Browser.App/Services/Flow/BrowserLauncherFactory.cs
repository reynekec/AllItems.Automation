using AllItems.Automation.Browser.Core.Abstractions;
using AllItems.Automation.Browser.Core.Browser;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;

namespace AllItems.Automation.Browser.App.Services.Flow;

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
