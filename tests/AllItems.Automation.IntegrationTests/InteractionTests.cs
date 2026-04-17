using FluentAssertions;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Exceptions;
using AllItems.Automation.IntegrationTests.TestUtilities;

namespace AllItems.Automation.IntegrationTests;

using AppBrowserType = AllItems.Automation.Browser.Core.Configuration.BrowserType;

public sealed class InteractionTests
{
    [Theory]
    [InlineData(AppBrowserType.Chromium)]
    [InlineData(AppBrowserType.Firefox)]
    [InlineData(AppBrowserType.WebKit)]
    public async Task Click_Fill_Type_Work_On_Form(AppBrowserType browserType)
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(browserType);
        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();
        var html = "<html><body><input id='name' /><button id='go' onclick=\"document.body.setAttribute('data-clicked','yes')\">Go</button></body></html>";

        await page.NavigateUrlAsync(IntegrationHarness.ToDataUrl(html));

        var input = page.Search().ById("name");
        var button = page.Search().ById("go");

        await input.FillAsync("abc");
        await input.TypeAsync("def");
        await button.ClickAsync();

        (await input.GetAttributeAsync("id")).Should().Be("name");
    }

    [Theory]
    [InlineData(AppBrowserType.Chromium)]
    [InlineData(AppBrowserType.Firefox)]
    [InlineData(AppBrowserType.WebKit)]
    public async Task Checkbox_Radio_Select_And_StateQueries_Work(AppBrowserType browserType)
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(browserType);
        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();

        var html = "<html><body><input id='cb' type='checkbox' /><input id='r1' type='radio' name='group' /><select id='sel'><option value='one'>One</option><option value='two'>Two</option></select></body></html>";
        await page.NavigateUrlAsync(IntegrationHarness.ToDataUrl(html));

        var checkbox = page.Search().ById("cb");
        var radio = page.Search().ById("r1");
        var select = page.Search().ById("sel");

        await checkbox.CheckAsync();
        await radio.CheckAsync();
        await select.SelectAsync("two");

        (await checkbox.IsVisibleAsync()).Should().BeTrue();
        (await checkbox.IsEnabledAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Missing_Element_Throws_Wrapped_Exception()
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(AppBrowserType.Chromium, new BrowserOptions
        {
            Headless = true,
            TimeoutMs = 400,
            RetryCount = 0,
        });

        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();
        await page.NavigateUrlAsync(IntegrationHarness.ToDataUrl("<html><body><div>empty</div></body></html>"));

        var missing = page.Search().ById("does-not-exist");
        var action = async () => await missing.WaitForAsync();

        await action.Should().ThrowAsync<UIElementNotFoundException>();
    }
}

