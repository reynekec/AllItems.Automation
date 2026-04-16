using Microsoft.Playwright;
using AllItems.Automation.Browser.Core.Abstractions;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AllItems.Automation.Browser.Core.Exceptions;
using AppBrowserType = AllItems.Automation.Browser.Core.Configuration.BrowserType;

namespace AllItems.Automation.Browser.Core.Browser;

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

            var contextOptions = new BrowserNewContextOptions();
            if (!options.Headless)
            {
                // Let the viewport follow the native window size for headed sessions.
                contextOptions.ViewportSize = ViewportSize.NoViewport;
            }

            if (options.HttpCredentials is not null)
            {
                contextOptions.HttpCredentials = options.HttpCredentials;
            }

            if (options.ClientCertificates is { Count: > 0 })
            {
                contextOptions.ClientCertificates = [.. options.ClientCertificates];
            }

            var context = await browser.NewContextAsync(contextOptions);

            if (options.ExtraHttpHeaders is { Count: > 0 })
            {
                await context.SetExtraHTTPHeadersAsync(options.ExtraHttpHeaders);
            }

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
