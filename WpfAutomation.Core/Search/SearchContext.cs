using Microsoft.Playwright;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;
using WpfAutomation.Core.Elements;

namespace WpfAutomation.Core.Search;

public sealed class SearchContext : ISearchContext
{
    private readonly IPage _page;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly ScreenshotService _screenshotService;
    private readonly BrowserOptions _options;

    public SearchContext(
        IPage page,
        DiagnosticsService diagnosticsService,
        ScreenshotService screenshotService,
        BrowserOptions options)
    {
        _page = page;
        _diagnosticsService = diagnosticsService;
        _screenshotService = screenshotService;
        _options = options;
    }

    public IUIElement ById(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info($"Search -> ById({id})");
        var selector = SelectorBuilder.ById(id);
        return CreateElement(_page.Locator(selector), selector);
    }

    public IUIElement ByCss(string selector, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info($"Search -> ByCss({selector})");
        return CreateElement(_page.Locator(selector), selector);
    }

    public IUIElement ByRole(string role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info($"Search -> ByRole({role})");

        if (!Enum.TryParse<AriaRole>(role, true, out var parsedRole))
        {
            throw new ArgumentException($"Invalid role value '{role}'.", nameof(role));
        }

        return CreateElement(_page.GetByRole(parsedRole), $"role={role}");
    }

    public IUIElement ByText(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info($"Search -> ByText({text})");
        return CreateElement(_page.GetByText(text), $"text={text}");
    }

    public IUIElement ByLabel(string label, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info($"Search -> ByLabel({label})");
        return CreateElement(_page.GetByLabel(label), $"label={label}");
    }

    public IUIElement ByPlaceholder(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info($"Search -> ByPlaceholder({text})");
        return CreateElement(_page.GetByPlaceholder(text), $"placeholder={text}");
    }

    public IUIElement ByTitle(string title, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info($"Search -> ByTitle({title})");
        return CreateElement(_page.GetByTitle(title), $"title={title}");
    }

    public IUIElement ByTestId(string testId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info($"Search -> ByTestId({testId})");
        return CreateElement(_page.GetByTestId(testId), $"testid={testId}");
    }

    private IUIElement CreateElement(ILocator locator, string selectorDescription)
    {
        return new UIElement(locator, _diagnosticsService, _screenshotService, _options, selectorDescription, _page);
    }
}