using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AllItems.Automation.Browser.Core.Elements;
using AllItems.Automation.Browser.Core.Exceptions;
using AllItems.Automation.Browser.Core.Search;

namespace WpfAutomation.Core.Tests;

public sealed class SearchAndElementTests
{
    [Fact]
    public void SearchMethods_ReturnWrappedElementForAllSelectors()
    {
        var mockPage = new Mock<IPage>();
        var mockLocator = new Mock<ILocator>();

        mockPage.Setup(page => page.Locator(It.IsAny<string>(), It.IsAny<PageLocatorOptions>())).Returns(mockLocator.Object);
        mockPage.Setup(page => page.GetByRole(It.IsAny<AriaRole>(), It.IsAny<PageGetByRoleOptions>())).Returns(mockLocator.Object);
        mockPage.Setup(page => page.GetByText(It.IsAny<string>(), It.IsAny<PageGetByTextOptions>())).Returns(mockLocator.Object);
        mockPage.Setup(page => page.GetByLabel(It.IsAny<string>(), It.IsAny<PageGetByLabelOptions>())).Returns(mockLocator.Object);
        mockPage.Setup(page => page.GetByPlaceholder(It.IsAny<string>(), It.IsAny<PageGetByPlaceholderOptions>())).Returns(mockLocator.Object);
        mockPage.Setup(page => page.GetByTitle(It.IsAny<string>(), It.IsAny<PageGetByTitleOptions>())).Returns(mockLocator.Object);
        mockPage.Setup(page => page.GetByTestId(It.IsAny<string>())).Returns(mockLocator.Object);

        var searchContext = CreateSearchContext(mockPage.Object);

        searchContext.ById("btn").Should().NotBeNull();
        searchContext.ByCss(".card").Should().NotBeNull();
        searchContext.ByRole("Button").Should().NotBeNull();
        searchContext.ByText("Submit").Should().NotBeNull();
        searchContext.ByLabel("Email").Should().NotBeNull();
        searchContext.ByPlaceholder("Enter value").Should().NotBeNull();
        searchContext.ByTitle("Tooltip").Should().NotBeNull();
        searchContext.ByTestId("save-btn").Should().NotBeNull();
    }

    [Fact]
    public async Task ElementInteractions_ClickFillType_Succeed()
    {
        var element = CreateElement(new Mock<ILocator>(MockBehavior.Strict), out var locator, options: new BrowserOptions { TimeoutMs = 1234 });

        locator
            .Setup(l => l.ClickAsync(It.Is<LocatorClickOptions>(o => o.Timeout == 1234)))
            .Returns(Task.CompletedTask);
        locator
            .Setup(l => l.FillAsync("value", It.Is<LocatorFillOptions>(o => o.Timeout == 1234)))
            .Returns(Task.CompletedTask);
        locator
            .Setup(l => l.TypeAsync("text", It.Is<LocatorTypeOptions>(o => o.Timeout == 1234)))
            .Returns(Task.CompletedTask);

        await element.ClickAsync();
        await element.FillAsync("value");
        await element.TypeAsync("text");
    }

    [Fact]
    public async Task ElementStateQueries_ReturnVisibilityAndEnabledState()
    {
        var element = CreateElement(new Mock<ILocator>(MockBehavior.Strict), out var locator, options: new BrowserOptions { TimeoutMs = 777 });

        locator
            .Setup(l => l.IsVisibleAsync(It.Is<LocatorIsVisibleOptions>(o => o.Timeout == 777)))
            .ReturnsAsync(true);
        locator
            .Setup(l => l.IsEnabledAsync(It.Is<LocatorIsEnabledOptions>(o => o.Timeout == 777)))
            .ReturnsAsync(false);

        (await element.IsVisibleAsync()).Should().BeTrue();
        (await element.IsEnabledAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task WaitForAsync_ThrowsElementNotFoundException_OnTimeout()
    {
        var element = CreateElement(new Mock<ILocator>(MockBehavior.Strict), out var locator, options: new BrowserOptions { TimeoutMs = 100, RetryCount = 0 });

        locator
            .Setup(l => l.WaitForAsync(It.IsAny<LocatorWaitForOptions>()))
            .ThrowsAsync(new TimeoutException("timeout"));
        locator
            .Setup(l => l.ScreenshotAsync(It.IsAny<LocatorScreenshotOptions>()))
            .ReturnsAsync(Array.Empty<byte>());

        var action = async () => await element.WaitForAsync();

        await action.Should().ThrowAsync<UIElementNotFoundException>();
    }

    [Fact]
    public async Task ClickAsync_CapturesScreenshotAndWrapsException_OnFailure()
    {
        var screenshotDirectory = Path.Combine(Path.GetTempPath(), $"wpf-automation-phase4-{Guid.NewGuid():N}");
        var element = CreateElement(
            new Mock<ILocator>(MockBehavior.Strict),
            out var locator,
            options: new BrowserOptions { TimeoutMs = 5000, RetryCount = 0, ScreenshotDirectory = screenshotDirectory },
            selector: "#missing");

        locator
            .Setup(l => l.ClickAsync(It.IsAny<LocatorClickOptions>()))
            .ThrowsAsync(new TimeoutException("timeout"));
        locator
            .Setup(l => l.ScreenshotAsync(It.IsAny<LocatorScreenshotOptions>()))
            .ReturnsAsync(Array.Empty<byte>());

        var action = async () => await element.ClickAsync();
        var exception = await action.Should().ThrowAsync<ElementInteractionException>();

        exception.Which.ScreenshotPath.Should().NotBeNullOrWhiteSpace();
        exception.Which.Selector.Should().Be("#missing");

        if (Directory.Exists(screenshotDirectory))
        {
            Directory.Delete(screenshotDirectory, true);
        }
    }

    [Fact]
    public async Task ClickAsync_AppliesRetryLogic()
    {
        var element = CreateElement(
            new Mock<ILocator>(MockBehavior.Strict),
            out var locator,
            options: new BrowserOptions { TimeoutMs = 5000, RetryCount = 2 });

        var attempts = 0;
        locator
            .Setup(l => l.ClickAsync(It.IsAny<LocatorClickOptions>()))
            .Returns(() =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new TimeoutException("transient");
                }

                return Task.CompletedTask;
            });

        await element.ClickAsync();

        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ClickAsync_WritesActionLogs()
    {
        var diagnostics = new DiagnosticsService();
        var screenshot = new ScreenshotService(new BrowserOptions());
        var options = new BrowserOptions { TimeoutMs = 1234, RetryCount = 0 };
        var locator = new Mock<ILocator>(MockBehavior.Strict);
        locator
            .Setup(l => l.ClickAsync(It.IsAny<LocatorClickOptions>()))
            .Returns(Task.CompletedTask);

        var element = new UIElement(locator.Object, diagnostics, screenshot, options, "#btn");

        await element.ClickAsync();

        var logs = diagnostics.GetLogs();
        logs.Should().Contain(log => log.Message.Contains("Click start"));
        logs.Should().Contain(log => log.Message.Contains("Click complete"));
    }

    private static SearchContext CreateSearchContext(IPage page)
    {
        return new SearchContext(
            page,
            new DiagnosticsService(),
            new ScreenshotService(new BrowserOptions()),
            new BrowserOptions());
    }

    private static UIElement CreateElement(
        Mock<ILocator> locator,
        out Mock<ILocator> outputLocator,
        BrowserOptions? options = null,
        string selector = "#test")
    {
        outputLocator = locator;
        return new UIElement(
            locator.Object,
            new DiagnosticsService(),
            new ScreenshotService(options ?? new BrowserOptions()),
            options ?? new BrowserOptions(),
            selector);
    }
}