using Microsoft.Playwright;
using AllItems.Automation.Browser.Core.Abstractions;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AllItems.Automation.Browser.Core.Exceptions;
using AllItems.Automation.Browser.Core.Inspection;
using AllItems.Automation.Browser.Core.Reports;

namespace AllItems.Automation.Browser.Core.Elements;

public sealed class UIElement : IUIElement
{
    private readonly ILocator _locator;
    private readonly IPage? _page;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly ScreenshotService _screenshotService;
    private readonly BrowserOptions _options;
    private readonly string _selectorDescription;
    private readonly ElementActionExecutor _executor;

    public UIElement(
        ILocator locator,
        DiagnosticsService diagnosticsService,
        ScreenshotService screenshotService,
        BrowserOptions? options = null,
        string? selectorDescription = null,
        IPage? page = null)
    {
        _locator = locator;
        _page = page;
        _diagnosticsService = diagnosticsService;
        _screenshotService = screenshotService;
        _options = options ?? new BrowserOptions();
        _selectorDescription = selectorDescription ?? "unknown";
        _executor = new ElementActionExecutor(_options, diagnosticsService);
    }

    internal ILocator Locator => _locator;

    internal string SelectorDescription => _selectorDescription;

    public Task ClickAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "Click",
            () => _locator.ClickAsync(new LocatorClickOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("Click", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task TypeAsync(string text, CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "Type",
            () => _locator.TypeAsync(text, new LocatorTypeOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("Type", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task FillAsync(string text, CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "Fill",
            () => _locator.FillAsync(text, new LocatorFillOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("Fill", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task<string> GetTextAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "GetText",
            async () => await _locator.TextContentAsync(new LocatorTextContentOptions { Timeout = _options.TimeoutMs }) ?? string.Empty,
            (exception, screenshotPath) => CreateInteractionException("GetText", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task<string?> GetAttributeAsync(string name, CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "GetAttribute",
            () => _locator.GetAttributeAsync(name, new LocatorGetAttributeOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("GetAttribute", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "IsVisible",
            () => _locator.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("IsVisible", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "IsEnabled",
            () => _locator.IsEnabledAsync(new LocatorIsEnabledOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("IsEnabled", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task HoverAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "Hover",
            () => _locator.HoverAsync(new LocatorHoverOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("Hover", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task CheckAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "Check",
            () => _locator.CheckAsync(new LocatorCheckOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("Check", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task UncheckAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "Uncheck",
            () => _locator.UncheckAsync(new LocatorUncheckOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("Uncheck", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task SelectAsync(string value, CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "Select",
            () => _locator.SelectOptionAsync(value, new LocatorSelectOptionOptions { Timeout = _options.TimeoutMs }),
            (exception, screenshotPath) => CreateInteractionException("Select", exception, screenshotPath),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public Task WaitForAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            "WaitFor",
            () => _locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = _options.TimeoutMs,
            }),
            (exception, screenshotPath) => new UIElementNotFoundException(
                "UI element was not found before timeout.",
                actionName: "WaitFor",
                selector: _selectorDescription,
                timeoutMs: _options.TimeoutMs,
                screenshotPath: screenshotPath,
                innerException: exception),
            CaptureFailureScreenshotAsync,
            cancellationToken);
    }

    public async Task<InspectionReport> InspectAsync(InspectOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_page is null)
        {
            throw new InspectionException(
                "Element inspection is unavailable because page context is missing.",
                actionName: "InspectElement",
                selector: _selectorDescription);
        }

        var inspector = new ElementInspector(_page, _options, _diagnosticsService, _screenshotService);
        return await inspector.InspectAsync(this, options, cancellationToken);
    }

    private Task<string?> CaptureFailureScreenshotAsync()
    {
        return _screenshotService.CaptureElementAsync(_locator);
    }

    private ElementInteractionException CreateInteractionException(string actionName, Exception exception, string? screenshotPath)
    {
        return new ElementInteractionException(
            $"{actionName} action failed.",
            actionName: actionName,
            selector: _selectorDescription,
            timeoutMs: _options.TimeoutMs,
            screenshotPath: screenshotPath,
            innerException: exception);
    }
}