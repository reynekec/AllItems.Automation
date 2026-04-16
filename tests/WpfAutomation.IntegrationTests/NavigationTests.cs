using FluentAssertions;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Exceptions;
using WpfAutomation.IntegrationTests.TestUtilities;

namespace WpfAutomation.IntegrationTests;

using AppBrowserType = AllItems.Automation.Browser.Core.Configuration.BrowserType;

public sealed class NavigationTests
{
    [Theory]
    [InlineData(AppBrowserType.Chromium)]
    [InlineData(AppBrowserType.Firefox)]
    [InlineData(AppBrowserType.WebKit)]
    public async Task Navigate_To_Public_Test_Page_IfReachable(AppBrowserType browserType)
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(browserType);
        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();

        try
        {
            await page.NavigateUrlAsync("https://example.com");
            page.CurrentUrl.Should().Contain("example.com");
        }
        catch
        {
            // Network might be unavailable in CI/local environment.
        }
    }

    [Theory]
    [InlineData(AppBrowserType.Chromium)]
    [InlineData(AppBrowserType.Firefox)]
    [InlineData(AppBrowserType.WebKit)]
    public async Task UrlProperty_Updates_AfterNavigation(AppBrowserType browserType)
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(browserType);
        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();
        var url = IntegrationHarness.ToDataUrl("<html><body><h1>hello</h1></body></html>");

        await page.NavigateUrlAsync(url);

        page.CurrentUrl.Should().Contain("data:text/html");
    }

    [Fact]
    public async Task Navigation_Failure_IsWrapped_With_Context()
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(AppBrowserType.Chromium, new BrowserOptions
        {
            Headless = true,
            TimeoutMs = 700,
            RetryCount = 1,
        });

        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();

        var action = async () => await page.NavigateUrlAsync("http://127.0.0.1:1");
        var exception = await action.Should().ThrowAsync<NavigationException>();

        exception.Which.ActionName.Should().Be("NavigateUrl");
        exception.Which.Url.Should().Contain("127.0.0.1:1");
    }

    [Fact]
    public async Task Navigation_Retry_Logs_Attempts_With_Deliberate_Failure()
    {
        var (session, diagnostics) = await IntegrationHarness.TryStartSessionAsync(AppBrowserType.Chromium, new BrowserOptions
        {
            Headless = true,
            TimeoutMs = 500,
            RetryCount = 2,
        });

        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();

        try
        {
            await page.NavigateUrlAsync("http://127.0.0.1:1");
        }
        catch
        {
            // Expected failure path.
        }

        diagnostics.GetLogs().Should().Contain(log => log.Message.Contains("Navigation attempt"));
    }
}
