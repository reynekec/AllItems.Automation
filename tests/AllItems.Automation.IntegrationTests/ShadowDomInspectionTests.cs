using FluentAssertions;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Inspection;
using AllItems.Automation.IntegrationTests.TestUtilities;

namespace AllItems.Automation.IntegrationTests;

public sealed class ShadowDomInspectionTests
{
    [Fact]
    public async Task ElementInspector_Traverses_ShadowRoot()
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(AllItems.Automation.Browser.Core.Configuration.BrowserType.Chromium, new BrowserOptions
        {
            Headless = true,
            TimeoutMs = 5000,
            RetryCount = 1,
        });

        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();

        var html = "<html><body><div id='host'></div><script>const host = document.getElementById('host'); const shadow = host.attachShadow({mode:'open'}); const item = document.createElement('span'); item.id='inside'; item.textContent='shadow'; shadow.appendChild(item);</script></body></html>";
        await page.NavigateUrlAsync(IntegrationHarness.ToDataUrl(html));

        var report = await page.Search().ById("host").InspectAsync(new InspectOptions
        {
            IncludeShadowDom = true,
            IncludeFrames = false,
        });

        report.RootElement.Should().NotBeNull();
        report.RootElement!.IsShadowHost.Should().BeTrue();
        report.RootElement.ShadowChildren.Should().NotBeEmpty();
    }
}

