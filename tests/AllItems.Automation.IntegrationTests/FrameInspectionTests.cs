using FluentAssertions;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Inspection;
using AllItems.Automation.IntegrationTests.TestUtilities;

namespace AllItems.Automation.IntegrationTests;

public sealed class FrameInspectionTests
{
    [Fact]
    public async Task PageInspector_Captures_Main_And_Frame_Content()
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(AllItems.Automation.Browser.Core.Configuration.BrowserType.Chromium, new BrowserOptions
        {
            Headless = true,
            TimeoutMs = 4000,
            RetryCount = 1,
        });

        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();

        var html = "<html><body><main id='main'>Main</main><iframe name='inner' srcdoc='<html><body><section id=\"frame-node\">Frame</section></body></html>'></iframe></body></html>";
        await page.NavigateUrlAsync(IntegrationHarness.ToDataUrl(html));

        var report = await page.InspectPage().InspectAsync(new PageInspectOptions
        {
            IncludeFrames = true,
            IncludeShadowDom = true,
        });

        report.MainRoot.Should().NotBeNull();
        report.Frames.Should().Contain(frame => frame.Name.Contains("inner"));
        report.Frames.SelectMany(frame => frame.RootNodes).Should().NotBeEmpty();
    }
}

