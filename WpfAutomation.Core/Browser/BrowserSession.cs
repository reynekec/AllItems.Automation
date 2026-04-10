using Microsoft.Playwright;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;
using WpfAutomation.Core.Page;

namespace WpfAutomation.Core.Browser;

public sealed class BrowserSession : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _context;
    private readonly BrowserOptions _options;
    private readonly DiagnosticsService _diagnosticsService;
    private bool _isClosed;

    public BrowserSession(
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        BrowserOptions options,
        DiagnosticsService diagnosticsService)
    {
        _playwright = playwright;
        _browser = browser;
        _context = context;
        _options = options;
        _diagnosticsService = diagnosticsService;
    }

    internal BrowserOptions Options => _options;

    public async Task<IPageWrapper> NewPageAsync()
    {
        EnsureOpen();
        _diagnosticsService.Info("Create page");

        var page = await _context.NewPageAsync();
        return new PageWrapper(page, this, _diagnosticsService);
    }

    public Task<IReadOnlyList<IPageWrapper>> GetPagesAsync()
    {
        EnsureOpen();

        var wrappedPages = _context.Pages
            .Select(page => (IPageWrapper)new PageWrapper(page, this, _diagnosticsService))
            .ToList()
            .AsReadOnly();

        return Task.FromResult((IReadOnlyList<IPageWrapper>)wrappedPages);
    }

    public Task SetExtraHttpHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        EnsureOpen();
        return _context.SetExtraHTTPHeadersAsync(headers);
    }

    public Task WithTemporaryOptionsAsync(int? timeoutMs, bool? navigationWaitUntilNetworkIdle, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return WithTemporaryOptionsAsync<object?>(
            timeoutMs,
            navigationWaitUntilNetworkIdle,
            async () =>
            {
                await action();
                return null;
            });
    }

    public async Task<TResult> WithTemporaryOptionsAsync<TResult>(
        int? timeoutMs,
        bool? navigationWaitUntilNetworkIdle,
        Func<Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureOpen();

        var originalTimeoutMs = _options.TimeoutMs;
        var originalNavigationWaitUntilNetworkIdle = _options.NavigationWaitUntilNetworkIdle;

        if (timeoutMs.HasValue)
        {
            _options.TimeoutMs = timeoutMs.Value;
        }

        if (navigationWaitUntilNetworkIdle.HasValue)
        {
            _options.NavigationWaitUntilNetworkIdle = navigationWaitUntilNetworkIdle.Value;
        }

        try
        {
            return await action();
        }
        finally
        {
            _options.TimeoutMs = originalTimeoutMs;
            _options.NavigationWaitUntilNetworkIdle = originalNavigationWaitUntilNetworkIdle;
        }
    }

    public async Task CloseAsync()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        _diagnosticsService.Info("Close session");

        try
        {
            await _context.CloseAsync();
        }
        catch
        {
            // Keep cleanup resilient to partial teardown failures.
        }

        try
        {
            await _browser.CloseAsync();
        }
        catch
        {
            // Keep cleanup resilient to partial teardown failures.
        }

        _playwright.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }

    private void EnsureOpen()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException("Browser session is already closed.");
        }
    }
}
