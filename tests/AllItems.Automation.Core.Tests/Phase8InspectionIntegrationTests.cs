using FluentAssertions;
using Microsoft.Playwright;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AllItems.Automation.Browser.Core.Elements;
using AllItems.Automation.Browser.Core.Inspection;

namespace AllItems.Automation.Core.Tests;

public sealed class Phase8InspectionIntegrationTests
{
    [Fact]
    public async Task ElementInspector_Returns_Dom_Structure()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;
        await page.SetContentAsync("<div id='root'><article id='target'><span>child</span></article></div>");

        var options = new BrowserOptions();
        var diagnostics = new DiagnosticsService();
        var screenshot = new ScreenshotService(options);
        var inspector = new ElementInspector(page, options, diagnostics, screenshot);
        var element = new UIElement(page.Locator("#target"), diagnostics, screenshot, options, "#target", page);

        var report = await inspector.InspectAsync(element, new InspectOptions { IncludeFrames = false });

        report.RootElement.Should().NotBeNull();
        report.RootElement!.TagName.Should().Be("article");
        report.RootElement.Children.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ElementInspector_Includes_Frames_When_Enabled()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;
        await page.SetContentAsync("<div id='target'>root</div><iframe name='child-frame' srcdoc='<html><body><div id=\"inside\">frame</div></body></html>'></iframe>");

        var options = new BrowserOptions();
        var diagnostics = new DiagnosticsService();
        var screenshot = new ScreenshotService(options);
        var inspector = new ElementInspector(page, options, diagnostics, screenshot);
        var element = new UIElement(page.Locator("#target"), diagnostics, screenshot, options, "#target", page);

        var report = await inspector.InspectAsync(element, new InspectOptions { IncludeFrames = true });

        report.Frames.Should().NotBeEmpty();
        report.Frames.Should().Contain(frame => frame.Name.Contains("child-frame"));
    }

    [Fact]
    public async Task ElementInspector_Includes_ShadowDom_When_Enabled()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;
        await page.SetContentAsync("<div id='host'></div>");
        await page.EvaluateAsync("() => { const host = document.getElementById('host'); const root = host.attachShadow({mode:'open'}); const span = document.createElement('span'); span.id='shadow-item'; span.textContent='value'; root.appendChild(span); }");

        var options = new BrowserOptions();
        var diagnostics = new DiagnosticsService();
        var screenshot = new ScreenshotService(options);
        var inspector = new ElementInspector(page, options, diagnostics, screenshot);
        var element = new UIElement(page.Locator("#host"), diagnostics, screenshot, options, "#host", page);

        var report = await inspector.InspectAsync(element, new InspectOptions { IncludeShadowDom = true, IncludeFrames = false });

        report.RootElement.Should().NotBeNull();
        report.RootElement!.IsShadowHost.Should().BeTrue();
        report.RootElement.ShadowChildren.Should().HaveCount(1);
    }

    [Fact]
    public async Task ElementInspection_Captures_Screenshot_And_Exports_Json()
    {
        var screenshotDir = Path.Combine(Path.GetTempPath(), $"phase8-shot-{Guid.NewGuid():N}");
        var exportDir = Path.Combine(Path.GetTempPath(), $"phase8-export-{Guid.NewGuid():N}");

        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;
        await page.SetContentAsync("<button id='target'>Go</button>");

        var options = new BrowserOptions
        {
            ScreenshotDirectory = screenshotDir,
            InspectionExportDirectory = exportDir,
        };

        var diagnostics = new DiagnosticsService();
        var screenshot = new ScreenshotService(options);
        var element = new UIElement(page.Locator("#target"), diagnostics, screenshot, options, "#target", page);

        var report = await element.InspectAsync(new InspectOptions
        {
            IncludeScreenshot = true,
            ExportJson = true,
            IncludeFrames = false,
        });

        report.ScreenshotPath.Should().NotBeNullOrWhiteSpace();
        report.JsonExportPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(report.ScreenshotPath!).Should().BeTrue();
        File.Exists(report.JsonExportPath!).Should().BeTrue();

        if (Directory.Exists(screenshotDir))
        {
            Directory.Delete(screenshotDir, true);
        }

        if (Directory.Exists(exportDir))
        {
            Directory.Delete(exportDir, true);
        }
    }

    [Fact]
    public async Task PageInspector_Returns_Main_And_Frame_Hierarchy()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;
        await page.SetContentAsync("<main id='main-root'>x</main><iframe name='info' srcdoc='<html><body><section id=\"fr\">f</section></body></html>'></iframe>");

        var options = new BrowserOptions();
        var diagnostics = new DiagnosticsService();
        var screenshot = new ScreenshotService(options);
        var inspector = new PageInspector(page, options, diagnostics, screenshot);

        var report = await inspector.InspectAsync(new PageInspectOptions { IncludeFrames = true });

        report.MainRoot.Should().NotBeNull();
        report.Url.Should().NotBeNullOrWhiteSpace();
        report.Frames.Should().NotBeEmpty();
    }

    private sealed class PlaywrightHarness : IAsyncDisposable
    {
        private readonly IPlaywright? _playwright;
        private readonly IBrowser? _browser;

        private PlaywrightHarness(IPlaywright? playwright, IBrowser? browser, IPage? page)
        {
            _playwright = playwright;
            _browser = browser;
            Page = page;
        }

        public IPage? Page { get; }

        public bool IsAvailable => Page is not null;

        public static async Task<PlaywrightHarness> TryCreateAsync()
        {
            try
            {
                var playwright = await Playwright.CreateAsync();
                var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var page = await browser.NewPageAsync();
                return new PlaywrightHarness(playwright, browser, page);
            }
            catch (PlaywrightException)
            {
                return new PlaywrightHarness(null, null, null);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_browser is not null)
            {
                await _browser.CloseAsync();
            }

            _playwright?.Dispose();
        }
    }
}
