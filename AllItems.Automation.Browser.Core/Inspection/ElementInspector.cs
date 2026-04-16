using Microsoft.Playwright;
using AllItems.Automation.Browser.Core.Abstractions;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AllItems.Automation.Browser.Core.Elements;
using AllItems.Automation.Browser.Core.Exceptions;
using AllItems.Automation.Browser.Core.Reports;

namespace AllItems.Automation.Browser.Core.Inspection;

public sealed class ElementInspector : IElementInspector
{
    private readonly IPage _page;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly ScreenshotService _screenshotService;
    private readonly DomTraversalService _domTraversalService;
    private readonly InspectionSerializer _serializer;

    public ElementInspector(
        IPage page,
        BrowserOptions options,
        DiagnosticsService diagnosticsService,
        ScreenshotService screenshotService)
    {
        _page = page;
        _diagnosticsService = diagnosticsService;
        _screenshotService = screenshotService;
        _domTraversalService = new DomTraversalService();
        _serializer = new InspectionSerializer(options);
    }

    public async Task<InspectionReport> InspectAsync(IUIElement element, InspectOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (element is not UIElement concrete)
        {
            throw new InspectionException("Element implementation is not supported by the inspector.", actionName: "InspectElement");
        }

        options ??= new InspectOptions();
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info("Inspection start -> element", new Dictionary<string, string>
        {
            ["selector"] = concrete.SelectorDescription,
        });

        try
        {
            var root = await _domTraversalService.InspectElementAsync(concrete.Locator, cancellationToken);
            if (root is null)
            {
                throw new InspectionException(
                    "Element inspection returned no data.",
                    actionName: "InspectElement",
                    selector: concrete.SelectorDescription,
                    url: _page.Url);
            }

            if (!options.IncludeDescendants)
            {
                root.Children = Array.Empty<ElementNodeReport>();
            }

            if (!options.IncludeAttributes)
            {
                root.Attributes = new Dictionary<string, string>();
            }

            if (!options.IncludeComputedStyles)
            {
                root.Styles = new Dictionary<string, string>();
            }

            if (options.IncludeShadowDom)
            {
                var shadow = await _domTraversalService.InspectShadowDomAsync(concrete.Locator, cancellationToken);
                if (shadow is not null)
                {
                    root.ShadowChildren = shadow.ShadowChildren;
                    root.IsShadowHost = shadow.IsShadowHost;
                }
            }

            var report = new InspectionReport
            {
                Url = _page.Url,
                Selector = concrete.SelectorDescription,
                RootElement = root,
                Frames = options.IncludeFrames ? await InspectFramesAsync(cancellationToken) : Array.Empty<FrameReport>(),
                Accessibility = options.IncludeAccessibility
                    ? await _domTraversalService.GetAccessibilityAsync(concrete.Locator, cancellationToken)
                    : null,
            };

            if (options.IncludeScreenshot)
            {
                report.ScreenshotPath = await _screenshotService.CaptureElementAsync(concrete.Locator, "inspection-element");
            }

            if (options.ExportJson)
            {
                report.JsonExportPath = await _serializer.ExportJsonAsync(report, "element-inspection");
            }

            _diagnosticsService.Info("Inspection complete -> element", new Dictionary<string, string>
            {
                ["selector"] = concrete.SelectorDescription,
            });

            return report;
        }
        catch (OperationCanceledException)
        {
            _diagnosticsService.Warn("Inspection cancelled", new Dictionary<string, string>
            {
                ["selector"] = concrete.SelectorDescription,
            });
            throw;
        }
        catch (InspectionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var screenshotPath = await _screenshotService.CaptureElementAsync(concrete.Locator, "inspection-failure");
            _diagnosticsService.Error("Inspection failed", exception);
            throw new InspectionException(
                "Element inspection failed.",
                actionName: "InspectElement",
                url: _page.Url,
                selector: concrete.SelectorDescription,
                screenshotPath: screenshotPath,
                innerException: exception);
        }
    }

    private async Task<IReadOnlyList<FrameReport>> InspectFramesAsync(CancellationToken cancellationToken)
    {
        var reports = new List<FrameReport>();

        foreach (var frame in _page.Frames.Where(frame => frame != _page.MainFrame))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var root = await _domTraversalService.InspectElementAsync(frame, "document.documentElement", cancellationToken);
            reports.Add(new FrameReport
            {
                Name = frame.Name ?? string.Empty,
                Url = frame.Url ?? string.Empty,
                ParentUrl = _page.Url,
                RootNodes = root is null ? Array.Empty<ElementNodeReport>() : [root],
            });
        }

        return reports;
    }
}
