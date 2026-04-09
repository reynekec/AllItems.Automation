using WpfAutomation.Core;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.App.Services;

public sealed class AutomationOrchestrator : IAutomationOrchestrator
{
    private readonly BrowserOptions _browserOptions;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly ScreenshotService _screenshotService;
    private BrowserSession? _activeSession;

    public AutomationOrchestrator(
        BrowserOptions browserOptions,
        DiagnosticsService diagnosticsService,
        ScreenshotService screenshotService)
    {
        _browserOptions = browserOptions;
        _diagnosticsService = diagnosticsService;
        _screenshotService = screenshotService;
    }

    public async Task<IPageWrapper> RunNavigationAsync(string url, CancellationToken cancellationToken)
    {
        await CloseActiveSessionAsync();

        BrowserSession? session = null;

        try
        {
            _diagnosticsService.Info($"Orchestrator run start -> {url}");
            session = await Automation.OpenBrowser(BrowserType.Chromium).StartAsync(_browserOptions, cancellationToken);
            var page = await session.NewPageAsync();
            var wrappedPage = await page.NavigateUrlAsync(url, cancellationToken);
            _activeSession = session;
            _diagnosticsService.Info("Orchestrator run complete");
            return wrappedPage;
        }
        catch (Exception exception)
        {
            _diagnosticsService.Error("Orchestrator run failed", exception);

            if (session is not null)
            {
                await session.CloseAsync();
            }

            throw;
        }
    }

    public async Task CloseActiveSessionAsync()
    {
        if (_activeSession is null)
        {
            return;
        }

        await _activeSession.CloseAsync();
        _activeSession = null;
        _diagnosticsService.Info("Orchestrator session closed");
    }
}
