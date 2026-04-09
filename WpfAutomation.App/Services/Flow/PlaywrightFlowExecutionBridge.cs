using System.Diagnostics;
using System.Text.RegularExpressions;
using WpfAutomation.App.Models.Flow;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.App.Services.Flow;

public sealed class PlaywrightFlowExecutionBridge : IFlowExecutionBridge
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IFlowRuntimeExecutor _runtimeExecutor;
    private readonly IBrowserLauncherFactory _browserLauncherFactory;
    private readonly BrowserOptions _baseBrowserOptions;
    private readonly DiagnosticsService _diagnosticsService;
    private BrowserSession? _activeSession;
    private IPageWrapper? _currentPage;

    public PlaywrightFlowExecutionBridge(
        IFlowRuntimeExecutor runtimeExecutor,
        IBrowserLauncherFactory browserLauncherFactory,
        BrowserOptions baseBrowserOptions,
        DiagnosticsService diagnosticsService)
    {
        _runtimeExecutor = runtimeExecutor;
        _browserLauncherFactory = browserLauncherFactory;
        _baseBrowserOptions = baseBrowserOptions;
        _diagnosticsService = diagnosticsService;
    }

    public async Task PrepareRunAsync(ExecutionFlowGraph executionGraph, bool forceHeaded = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionGraph);

        await CloseActiveSessionAsync();

        var runtimeResult = await _runtimeExecutor.ExecuteAsync(executionGraph, cancellationToken);
        var nodesBySourceId = executionGraph.Nodes.ToDictionary(node => node.SourceNodeId, StringComparer.Ordinal);

        foreach (var sourceNodeId in runtimeResult.ExecutedNodeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!nodesBySourceId.TryGetValue(sourceNodeId, out var node) || node.NodeKind != FlowNodeKind.Action)
            {
                continue;
            }

            await ExecuteActionAsync(node, forceHeaded, cancellationToken);
        }
    }

    public async Task CloseActiveSessionAsync()
    {
        _currentPage = null;

        if (_activeSession is null)
        {
            return;
        }

        await _activeSession.CloseAsync();
        _activeSession = null;
    }

    private async Task ExecuteActionAsync(IExecutionFlowNode node, bool forceHeaded, CancellationToken cancellationToken)
    {
        switch (node.ActionId)
        {
            case "open-browser":
                await OpenBrowserAsync(RequireParameters<OpenBrowserActionParameters>(node), forceHeaded, cancellationToken);
                return;
            case "new-page":
                await NewPageAsync(RequireParameters<NewPageActionParameters>(node), forceHeaded, cancellationToken);
                return;
            case "close-browser":
                await CloseBrowserAsync(RequireParameters<CloseBrowserActionParameters>(node));
                return;
            case "navigate-to-url":
                await NavigateToUrlAsync(RequireParameters<NavigateToUrlActionParameters>(node), forceHeaded, cancellationToken);
                return;
            case "click-element":
                await ClickElementAsync(RequireParameters<ClickElementActionParameters>(node), cancellationToken);
                return;
            case "fill-input":
                await FillInputAsync(RequireParameters<FillInputActionParameters>(node), cancellationToken);
                return;
            case "hover-element":
                await HoverElementAsync(RequireParameters<HoverElementActionParameters>(node), cancellationToken);
                return;
            case "select-option":
                await SelectOptionAsync(RequireParameters<SelectOptionActionParameters>(node), cancellationToken);
                return;
            case "expect-enabled":
                await ExpectEnabledAsync(RequireParameters<ExpectEnabledActionParameters>(node), cancellationToken);
                return;
            case "expect-hidden":
                await ExpectHiddenAsync(RequireParameters<ExpectHiddenActionParameters>(node), cancellationToken);
                return;
            case "expect-text":
                await ExpectTextAsync(RequireParameters<ExpectTextActionParameters>(node), cancellationToken);
                return;
            case "expect-visible":
                await ExpectVisibleAsync(RequireParameters<ExpectVisibleActionParameters>(node), cancellationToken);
                return;
            case "wait-for-url":
                await WaitForUrlAsync(RequireParameters<WaitForUrlActionParameters>(node), cancellationToken);
                return;
            default:
                throw new NotSupportedException($"Flow action '{node.ActionId}' is not yet bound to runtime execution.");
        }
    }

    private async Task OpenBrowserAsync(OpenBrowserActionParameters parameters, bool forceHeaded, CancellationToken cancellationToken)
    {
        await CloseActiveSessionAsync();

        var browserType = ResolveBrowserType(parameters.BrowserEngine);
        var options = CreateBrowserOptions(parameters, forceHeaded);
        var launcher = _browserLauncherFactory.Create(browserType);

        _diagnosticsService.Info("Flow open-browser start.", new Dictionary<string, string>
        {
            ["browserEngine"] = parameters.BrowserEngine,
            ["headless"] = options.Headless.ToString(),
            ["timeoutMs"] = options.TimeoutMs.ToString(),
            ["retryCount"] = options.RetryCount.ToString(),
        });

        _activeSession = await launcher.StartAsync(options, cancellationToken);
        _currentPage = await _activeSession.NewPageAsync();
    }

    private async Task NewPageAsync(NewPageActionParameters parameters, bool forceHeaded, CancellationToken cancellationToken)
    {
        var session = await EnsureSessionAsync(forceHeaded, cancellationToken);
        _currentPage = await session.NewPageAsync();

        if (!string.IsNullOrWhiteSpace(parameters.InitialUrl))
        {
            _currentPage = await _currentPage.NavigateUrlAsync(parameters.InitialUrl, cancellationToken);
        }
    }

    private async Task CloseBrowserAsync(CloseBrowserActionParameters _)
    {
        await CloseActiveSessionAsync();
    }

    private async Task NavigateToUrlAsync(NavigateToUrlActionParameters parameters, bool forceHeaded, CancellationToken cancellationToken)
    {
        var page = await EnsurePageAsync(forceHeaded, cancellationToken);
        _currentPage = await page.NavigateUrlAsync(parameters.Url, cancellationToken);
    }

    private async Task ClickElementAsync(ClickElementActionParameters parameters, CancellationToken cancellationToken)
    {
        EnsureUnsupportedParameter(string.IsNullOrWhiteSpace(parameters.FrameSelector), "click-element", "FrameSelector");
        EnsureUnsupportedParameter(!parameters.Force, "click-element", "Force");

        var element = (await RequirePageAsync(cancellationToken)).Search().ByCss(parameters.Selector, cancellationToken);
        await element.ClickAsync(cancellationToken);
    }

    private async Task FillInputAsync(FillInputActionParameters parameters, CancellationToken cancellationToken)
    {
        var element = (await RequirePageAsync(cancellationToken)).Search().ByCss(parameters.Selector, cancellationToken);

        if (parameters.ClearFirst)
        {
            await element.FillAsync(parameters.Value, cancellationToken);
            return;
        }

        await element.TypeAsync(parameters.Value, cancellationToken);
    }

    private async Task HoverElementAsync(HoverElementActionParameters parameters, CancellationToken cancellationToken)
    {
        var element = (await RequirePageAsync(cancellationToken)).Search().ByCss(parameters.Selector, cancellationToken);
        await element.HoverAsync(cancellationToken);
    }

    private async Task SelectOptionAsync(SelectOptionActionParameters parameters, CancellationToken cancellationToken)
    {
        var element = (await RequirePageAsync(cancellationToken)).Search().ByCss(parameters.Selector, cancellationToken);
        await element.SelectAsync(parameters.OptionValue, cancellationToken);
    }

    private async Task ExpectEnabledAsync(ExpectEnabledActionParameters parameters, CancellationToken cancellationToken)
    {
        var element = (await RequirePageAsync(cancellationToken)).Search().ByCss(parameters.Selector, cancellationToken);
        await WaitUntilAsync(() => element.IsEnabledAsync(cancellationToken), parameters.TimeoutMs, $"Element '{parameters.Selector}' did not become enabled.", cancellationToken);
    }

    private async Task ExpectHiddenAsync(ExpectHiddenActionParameters parameters, CancellationToken cancellationToken)
    {
        var element = (await RequirePageAsync(cancellationToken)).Search().ByCss(parameters.Selector, cancellationToken);
        await WaitUntilAsync(async () => !await element.IsVisibleAsync(cancellationToken), parameters.TimeoutMs, $"Element '{parameters.Selector}' did not become hidden.", cancellationToken);
    }

    private async Task ExpectTextAsync(ExpectTextActionParameters parameters, CancellationToken cancellationToken)
    {
        var element = (await RequirePageAsync(cancellationToken)).Search().ByCss(parameters.Selector, cancellationToken);
        await WaitUntilAsync(async () =>
        {
            var text = await element.GetTextAsync(cancellationToken);
            return string.Equals(text, parameters.ExpectedText,
                parameters.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }, parameters.TimeoutMs, $"Element '{parameters.Selector}' text did not match the expected value.", cancellationToken);
    }

    private async Task ExpectVisibleAsync(ExpectVisibleActionParameters parameters, CancellationToken cancellationToken)
    {
        var element = (await RequirePageAsync(cancellationToken)).Search().ByCss(parameters.Selector, cancellationToken);
        await WaitUntilAsync(() => element.IsVisibleAsync(cancellationToken), parameters.TimeoutMs, $"Element '{parameters.Selector}' did not become visible.", cancellationToken);
    }

    private async Task WaitForUrlAsync(WaitForUrlActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await WaitUntilAsync(
            () => Task.FromResult(UrlMatches(page.CurrentUrl, parameters.UrlPattern, parameters.IsRegex)),
            parameters.TimeoutMs,
            $"URL did not match '{parameters.UrlPattern}'.",
            cancellationToken);
    }

    private async Task<BrowserSession> EnsureSessionAsync(bool forceHeaded, CancellationToken cancellationToken)
    {
        if (_activeSession is not null)
        {
            return _activeSession;
        }

        var launcher = _browserLauncherFactory.Create(BrowserType.Chromium);
        var options = CloneBrowserOptions(forceHeaded);
        _activeSession = await launcher.StartAsync(options, cancellationToken);
        return _activeSession;
    }

    private async Task<IPageWrapper> EnsurePageAsync(bool forceHeaded, CancellationToken cancellationToken)
    {
        if (_currentPage is not null)
        {
            return _currentPage;
        }

        var session = await EnsureSessionAsync(forceHeaded, cancellationToken);
        _currentPage = await session.NewPageAsync();
        return _currentPage;
    }

    private async Task<IPageWrapper> RequirePageAsync(CancellationToken cancellationToken)
    {
        return await EnsurePageAsync(forceHeaded: false, cancellationToken);
    }

    private static TParameters RequireParameters<TParameters>(IExecutionFlowNode node)
        where TParameters : ActionParameters
    {
        return node.ActionParameters as TParameters
            ?? throw new InvalidOperationException($"Action '{node.ActionId}' is missing typed parameters of '{typeof(TParameters).Name}'.");
    }

    private static void EnsureUnsupportedParameter(bool condition, string actionId, string parameterName)
    {
        if (condition)
        {
            return;
        }

        throw new NotSupportedException($"Action '{actionId}' does not yet support the '{parameterName}' option at runtime.");
    }

    private BrowserOptions CreateBrowserOptions(OpenBrowserActionParameters parameters, bool forceHeaded)
    {
        var options = CloneBrowserOptions(forceHeaded);
        options.Headless = forceHeaded ? false : parameters.Headless;
        options.TimeoutMs = parameters.TimeoutMs;
        options.RetryCount = parameters.RetryCount;
        return options;
    }

    private BrowserOptions CloneBrowserOptions(bool forceHeaded)
    {
        return new BrowserOptions
        {
            Headless = forceHeaded ? false : _baseBrowserOptions.Headless,
            TimeoutMs = _baseBrowserOptions.TimeoutMs,
            RetryCount = _baseBrowserOptions.RetryCount,
            ScreenshotDirectory = _baseBrowserOptions.ScreenshotDirectory,
            InspectionExportDirectory = _baseBrowserOptions.InspectionExportDirectory,
        };
    }

    private static BrowserType ResolveBrowserType(string browserEngine)
    {
        return browserEngine.Trim().ToLowerInvariant() switch
        {
            "chromium" => BrowserType.Chromium,
            "firefox" => BrowserType.Firefox,
            "webkit" => BrowserType.WebKit,
            _ => throw new InvalidOperationException($"Unsupported browser engine '{browserEngine}'."),
        };
    }

    private static bool UrlMatches(string currentUrl, string pattern, bool isRegex)
    {
        if (isRegex)
        {
            return Regex.IsMatch(currentUrl, pattern);
        }

        return string.Equals(currentUrl, pattern, StringComparison.OrdinalIgnoreCase)
            || currentUrl.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, int timeoutMs, string failureMessage, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await predicate())
            {
                return;
            }

            var remaining = timeout - stopwatch.Elapsed;
            await Task.Delay(remaining < PollInterval ? remaining : PollInterval, cancellationToken);
        }

        throw new TimeoutException(failureMessage);
    }
}
