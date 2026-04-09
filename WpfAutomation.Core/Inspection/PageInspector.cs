using Microsoft.Playwright;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;
using WpfAutomation.Core.Exceptions;
using WpfAutomation.Core.Reports;

namespace WpfAutomation.Core.Inspection;

public sealed class PageInspector : IPageInspector
{
    private readonly IPage _page;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly ScreenshotService _screenshotService;
    private readonly DomTraversalService _domTraversalService;
    private readonly InspectionSerializer _serializer;

    public PageInspector(
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

    public async Task<PageInspectionReport> InspectAsync(PageInspectOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new PageInspectOptions();
        cancellationToken.ThrowIfCancellationRequested();
        _diagnosticsService.Info("Inspection start -> page", new Dictionary<string, string>
        {
            ["url"] = _page.Url,
        });

        try
        {
            var mainRoot = await _domTraversalService.InspectElementAsync(_page.MainFrame, "document.documentElement", cancellationToken);
            var frames = options.IncludeFrames
                ? await InspectFramesAsync(options, cancellationToken)
                : Array.Empty<FrameReport>();

            var report = new PageInspectionReport
            {
                Url = _page.Url,
                MainRoot = mainRoot,
                Frames = frames,
            };

            if (options.ExportJson)
            {
                report.JsonExportPath = await _serializer.ExportJsonAsync(report, "page-inspection");
            }

            _diagnosticsService.Info("Inspection complete -> page", new Dictionary<string, string>
            {
                ["url"] = _page.Url,
            });

            return report;
        }
        catch (OperationCanceledException)
        {
            _diagnosticsService.Warn("Page inspection cancelled", new Dictionary<string, string>
            {
                ["url"] = _page.Url,
            });
            throw;
        }
        catch (Exception exception)
        {
            var screenshotPath = await _screenshotService.CapturePageAsync(_page, "page-inspection-failure");
            _diagnosticsService.Error("Page inspection failed", exception);
            throw new InspectionException(
                "Page inspection failed.",
                actionName: "InspectPage",
                url: _page.Url,
                screenshotPath: screenshotPath,
                innerException: exception);
        }
    }

    private async Task<IReadOnlyList<FrameReport>> InspectFramesAsync(PageInspectOptions options, CancellationToken cancellationToken)
    {
        var reports = new List<FrameReport>();

        foreach (var frame in _page.Frames.Where(frame => frame != _page.MainFrame))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = await _domTraversalService.InspectElementAsync(frame, "document.documentElement", cancellationToken);

            if (root is not null && !options.IncludeShadowDom)
            {
                root.ShadowChildren = Array.Empty<ElementNodeReport>();
            }

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
