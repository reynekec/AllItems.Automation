using Microsoft.Playwright;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Diagnostics;
using WpfAutomation.Core.Exceptions;
using WpfAutomation.Core.Inspection;
using WpfAutomation.Core.Search;

namespace WpfAutomation.Core.Page;

public sealed class PageWrapper : IPageWrapper
{
    private readonly IPage _page;
    private readonly BrowserSession _session;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly NavigationService _navigationService;
    private readonly ScreenshotService _screenshotService;
    private string? _title;

    public PageWrapper(IPage page, BrowserSession session, DiagnosticsService diagnosticsService)
    {
        _page = page;
        _session = session;
        _diagnosticsService = diagnosticsService;
        _navigationService = new NavigationService(_session.Options, _diagnosticsService);
        _screenshotService = new ScreenshotService(_session.Options);
    }

    public string CurrentUrl => _page.Url;

    public string? Title => _title;

    public async Task<IPageWrapper> NavigateUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri))
        {
            throw new NavigationException(
                "Invalid URL format.",
                actionName: "NavigateUrl",
                url: url,
                timeoutMs: _session.Options.TimeoutMs);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _diagnosticsService.Info($"Navigate start -> {url}");

            await _navigationService.ExecuteAsync(async (_, _, token) =>
            {
                token.ThrowIfCancellationRequested();
                await _page.GotoAsync(parsedUri.ToString(), new PageGotoOptions
                {
                    Timeout = _session.Options.TimeoutMs,
                    WaitUntil = WaitUntilState.Load,
                });
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _title = await _page.TitleAsync();
            _diagnosticsService.Info($"Navigate complete -> {CurrentUrl}");
            return this;
        }
        catch (OperationCanceledException exception)
        {
            _diagnosticsService.Warn("Navigate cancelled", new Dictionary<string, string>
            {
                ["url"] = parsedUri.ToString(),
            });

            throw new NavigationException(
                "Navigation cancelled.",
                actionName: "NavigateUrl",
                url: parsedUri.ToString(),
                timeoutMs: _session.Options.TimeoutMs,
                innerException: exception);
        }
        catch (Exception exception)
        {
            var screenshotPath = await CaptureFailureScreenshotAsync();
            _diagnosticsService.Error("Navigate failed", exception);

            throw new NavigationException(
                "Navigation failed.",
                actionName: "NavigateUrl",
                url: parsedUri.ToString(),
                timeoutMs: _session.Options.TimeoutMs,
                screenshotPath: screenshotPath,
                innerException: exception);
        }
    }

    public ISearchContext Search()
    {
        return new SearchContext(
            _page,
            _diagnosticsService,
            new ScreenshotService(_session.Options),
            _session.Options);
    }

    public IPageInspector InspectPage()
    {
        return new PageInspector(_page, _session.Options, _diagnosticsService, _screenshotService);
    }

    private async Task<string?> CaptureFailureScreenshotAsync()
    {
        return await _screenshotService.CapturePageAsync(_page, "navigation-failure");
    }
}