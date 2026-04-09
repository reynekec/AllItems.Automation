using Microsoft.Playwright;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;
using WpfAutomation.Core.Exceptions;
using AppBrowserType = WpfAutomation.Core.Configuration.BrowserType;

namespace WpfAutomation.Core.Browser;

public sealed class BrowserLauncher : IBrowserLauncher
{
    private readonly AppBrowserType _browserType;
    private readonly Func<Task<IPlaywright>> _playwrightFactory;
    private readonly DiagnosticsService _diagnosticsService;

    public BrowserLauncher(
        AppBrowserType browserType,
        Func<Task<IPlaywright>>? playwrightFactory = null,
        DiagnosticsService? diagnosticsService = null)
    {
        _browserType = browserType;
        _playwrightFactory = playwrightFactory ?? Playwright.CreateAsync;
        _diagnosticsService = diagnosticsService ?? new DiagnosticsService();
    }

    public async Task<IPageWrapper> NavigateUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var session = await StartAsync(new BrowserOptions(), cancellationToken);
        var page = await session.NewPageAsync();
        return await page.NavigateUrlAsync(url, cancellationToken);
    }

    public async Task<BrowserSession> StartAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        IPlaywright? playwright = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _diagnosticsService.Info($"Launch -> {_browserType}");
            playwright = await _playwrightFactory();
            cancellationToken.ThrowIfCancellationRequested();

            var selectedBrowserType = ResolveBrowserType(playwright);
            var browser = await selectedBrowserType.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = options.Headless,
            });
            cancellationToken.ThrowIfCancellationRequested();

            var context = await browser.NewContextAsync(new BrowserNewContextOptions());
            _diagnosticsService.Info("Launch complete");

            return new BrowserSession(playwright, browser, context, options, _diagnosticsService);
        }
        catch (OperationCanceledException exception)
        {
            _diagnosticsService.Warn("Launch cancelled", new Dictionary<string, string>
            {
                ["action"] = "LaunchBrowser",
            });
            playwright?.Dispose();

            throw new AutomationException(
                "Browser launch cancelled.",
                actionName: "LaunchBrowser",
                timeoutMs: options.TimeoutMs,
                innerException: exception);
        }
        catch (Exception exception)
        {
            _diagnosticsService.Error("Launch failed", exception);
            playwright?.Dispose();

            throw new AutomationException(
                "Browser launch failed.",
                actionName: "LaunchBrowser",
                innerException: exception);
        }
    }

    private IBrowserType ResolveBrowserType(IPlaywright playwright)
    {
        return _browserType switch
        {
            AppBrowserType.Chromium => playwright.Chromium,
            AppBrowserType.Firefox => playwright.Firefox,
            AppBrowserType.WebKit => playwright.Webkit,
            _ => throw new AutomationException($"Unsupported browser type: {_browserType}", actionName: "ResolveBrowserType"),
        };
    }
}