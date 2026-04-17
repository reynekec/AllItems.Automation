using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;

namespace AllItems.Automation.Core.Tests;

public sealed class Phase7InspectionJavaScriptTests
{
    [Fact]
    public async Task BuildCssPathScript_Returns_Path()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;

        await page.SetContentAsync("<main><section><div class='box'><span id='target'>value</span></div></section></main>");
        var script = LoadScript("BuildCssPath.js");

        var result = await page.EvaluateAsync<string>($"() => {{ {script}; return buildCssPath(document.getElementById('target')); }}");

        result.Should().Contain("#target");
    }

    [Fact]
    public async Task BuildXPathScript_Returns_Path()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;

        await page.SetContentAsync("<main><section><button id='target'>click</button></section></main>");
        var script = LoadScript("BuildXPath.js");

        var result = await page.EvaluateAsync<string>($"() => {{ {script}; return buildXPath(document.getElementById('target')); }}");

        result.Should().Contain("@id='target'");
    }

    [Fact]
    public async Task GetComputedStylesScript_Returns_Subset()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;

        await page.SetContentAsync("<style>#target{display:block;color:rgb(255, 0, 0);font-size:14px;}</style><div id='target'>style</div>");
        var script = LoadScript("GetComputedStyles.js");

        var json = await page.EvaluateAsync<string>($"() => {{ {script}; const value = getComputedStylesSubset(document.getElementById('target')); return JSON.stringify(value); }}");
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("display", out var display).Should().BeTrue();
        display.GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.TryGetProperty("fontSize", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessibilityDataScript_Returns_Aria_Fields()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;

        await page.SetContentAsync("<button id='target' role='button' aria-label='Submit' aria-description='Submit form'>Submit</button>");
        var script = LoadScript("GetAccessibilityData.js");

        var json = await page.EvaluateAsync<string>($"() => {{ {script}; const value = getAccessibilityData(document.getElementById('target')); return JSON.stringify(value); }}");
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("role").GetString().Should().Be("button");
        doc.RootElement.GetProperty("ariaLabel").GetString().Should().Be("Submit");
    }

    [Fact]
    public async Task InspectShadowDomScript_Returns_Shadow_Structure()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;

        await page.SetContentAsync("<div id='host'></div>");
        await page.EvaluateAsync("() => { const host = document.getElementById('host'); const shadow = host.attachShadow({ mode: 'open' }); const child = document.createElement('span'); child.id = 'inside-shadow'; child.textContent = 'shadow text'; shadow.appendChild(child); }");

        var script = LoadScript("InspectShadowDom.js");
        var json = await page.EvaluateAsync<string>($"() => {{ {script}; const value = inspectShadowDom(document.getElementById('host')); return JSON.stringify(value); }}");
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("isShadowHost").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("shadowChildren").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task InspectElementScript_Returns_Nested_Node_Data()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;

        await page.SetContentAsync("<div id='root'><article id='target' class='card'><span>hello</span></article></div>");
        var script = LoadScript("InspectElement.js");

        var json = await page.EvaluateAsync<string>($"() => {{ {script}; const value = inspectElement(document.getElementById('target')); return JSON.stringify(value); }}");
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("tagName").GetString().Should().Be("article");
        doc.RootElement.GetProperty("id").GetString().Should().Be("target");
        doc.RootElement.GetProperty("children").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("cssPath").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("xPath").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InspectPageScript_Returns_Frame_Summary()
    {
        await using var harness = await PlaywrightHarness.TryCreateAsync();
        if (!harness.IsAvailable)
        {
            return;
        }

        var page = harness.Page!;

        await page.SetContentAsync("<div id='root'>page</div><iframe id='f1' name='frame-one' srcdoc='<html><body><p id=\"inside\">frame</p></body></html>'></iframe>");
        var script = LoadScript("InspectPage.js");

        var json = await page.EvaluateAsync<string>($"() => {{ {script}; const value = inspectPage(); return JSON.stringify(value); }}");
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("root", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("frames", out var frames).Should().BeTrue();
        frames.GetArrayLength().Should().Be(1);
    }

    private static string LoadScript(string fileName)
    {
        var root = ResolveRepositoryRoot();
        var scriptPath = Path.Combine(root, "AllItems.Automation.Browser.Core", "Inspection", "JavaScript", fileName);
        return File.ReadAllText(scriptPath);
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WpfAutomation.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
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
                var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                });
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
