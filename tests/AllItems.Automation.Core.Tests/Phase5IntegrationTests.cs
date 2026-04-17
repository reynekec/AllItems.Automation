using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using AllItems.Automation.Browser.Core.Browser;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AllItems.Automation.Browser.Core.Elements;
using AllItems.Automation.Browser.Core.Exceptions;
using AllItems.Automation.Browser.Core.Page;
using AppBrowserType = AllItems.Automation.Browser.Core.Configuration.BrowserType;

namespace AllItems.Automation.Core.Tests;

public sealed class Phase5IntegrationTests
{
    [Fact]
    public async Task StartAsync_Cancellation_IsWrappedWithDiagnosticsContext()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var launcher = new BrowserLauncher(
            AppBrowserType.Chromium,
            () => Task.FromResult(Mock.Of<IPlaywright>()),
            new DiagnosticsService());

        var action = async () => await launcher.StartAsync(new BrowserOptions(), cts.Token);

        var exception = await action.Should().ThrowAsync<AutomationException>();
        exception.Which.ActionName.Should().Be("LaunchBrowser");
        exception.Which.InnerException.Should().BeOfType<OperationCanceledException>();
    }

    [Fact]
    public async Task NavigateUrlAsync_Cancellation_IsWrapped()
    {
        var mockPage = new Mock<IPage>();
        var wrapper = CreateWrapper(mockPage, new BrowserOptions());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = async () => await wrapper.NavigateUrlAsync("https://cancel.example", cts.Token);
        var exception = await action.Should().ThrowAsync<NavigationException>();

        exception.Which.ActionName.Should().Be("NavigateUrl");
        exception.Which.InnerException.Should().BeOfType<OperationCanceledException>();
    }

    [Fact]
    public async Task ClickAsync_Cancellation_IsWrapped()
    {
        var locator = new Mock<ILocator>();
        var element = new UIElement(
            locator.Object,
            new DiagnosticsService(),
            new ScreenshotService(new BrowserOptions()),
            new BrowserOptions());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = async () => await element.ClickAsync(cts.Token);
        var exception = await action.Should().ThrowAsync<ElementInteractionException>();

        exception.Which.ActionName.Should().Be("Click");
        exception.Which.InnerException.Should().BeOfType<OperationCanceledException>();
    }

    [Fact]
    public async Task InteractionException_ContainsExpectedDiagnosticFields()
    {
        var screenshotDir = Path.Combine(Path.GetTempPath(), $"phase5-{Guid.NewGuid():N}");
        var locator = new Mock<ILocator>();
        locator
            .Setup(l => l.ClickAsync(It.IsAny<LocatorClickOptions>()))
            .ThrowsAsync(new TimeoutException("timeout"));
        locator
            .Setup(l => l.ScreenshotAsync(It.IsAny<LocatorScreenshotOptions>()))
            .ReturnsAsync(Array.Empty<byte>());

        var element = new UIElement(
            locator.Object,
            new DiagnosticsService(),
            new ScreenshotService(new BrowserOptions { ScreenshotDirectory = screenshotDir, TimeoutMs = 999, RetryCount = 0 }),
            new BrowserOptions { ScreenshotDirectory = screenshotDir, TimeoutMs = 999, RetryCount = 0 },
            "#btn");

        var action = async () => await element.ClickAsync();
        var exception = await action.Should().ThrowAsync<ElementInteractionException>();

        exception.Which.ActionName.Should().Be("Click");
        exception.Which.Selector.Should().Be("#btn");
        exception.Which.TimeoutMs.Should().Be(999);
        exception.Which.ScreenshotPath.Should().NotBeNullOrWhiteSpace();
        exception.Which.InnerException.Should().BeOfType<TimeoutException>();
        exception.Which.ToString().Should().Contain("Action: Click");

        if (Directory.Exists(screenshotDir))
        {
            Directory.Delete(screenshotDir, true);
        }
    }

    [Fact]
    public void DiagnosticsService_CanCaptureRetrieveAndClearStructuredLogs()
    {
        var diagnostics = new DiagnosticsService();

        diagnostics.Info("Info message", new Dictionary<string, string> { ["step"] = "1" });
        diagnostics.Warn("Warn message");
        diagnostics.Error("Error message", new InvalidOperationException("boom"));

        var logs = diagnostics.GetLogs();
        logs.Should().HaveCount(3);
        logs[0].Level.Should().Be("INFO");
        logs[0].ContextData.Should().ContainKey("step");
        logs[2].Message.Should().Contain("boom");

        diagnostics.ClearLogs();
        diagnostics.GetLogs().Should().BeEmpty();
    }

    [Fact]
    public async Task ScreenshotService_CaptureMethods_ReturnFullPath()
    {
        var screenshotDir = Path.Combine(Path.GetTempPath(), $"phase5-shot-{Guid.NewGuid():N}");
        var service = new ScreenshotService(new BrowserOptions { ScreenshotDirectory = screenshotDir });

        var page = new Mock<IPage>();
        page.Setup(p => p.ScreenshotAsync(It.IsAny<PageScreenshotOptions>())).ReturnsAsync(Array.Empty<byte>());

        var locator = new Mock<ILocator>();
        locator.Setup(l => l.ScreenshotAsync(It.IsAny<LocatorScreenshotOptions>())).ReturnsAsync(Array.Empty<byte>());

        var pagePath = await service.CapturePageAsync(page.Object, "page test");
        var elementPath = await service.CaptureElementAsync(locator.Object, "element test");

        pagePath.Should().NotBeNullOrWhiteSpace();
        elementPath.Should().NotBeNullOrWhiteSpace();
        Path.IsPathRooted(pagePath!).Should().BeTrue();
        Path.IsPathRooted(elementPath!).Should().BeTrue();

        if (Directory.Exists(screenshotDir))
        {
            Directory.Delete(screenshotDir, true);
        }
    }

    private static PageWrapper CreateWrapper(Mock<IPage> mockPage, BrowserOptions options)
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();

        mockContext
            .Setup(context => context.CloseAsync(It.IsAny<BrowserContextCloseOptions>()))
            .Returns(Task.CompletedTask);
        mockBrowser
            .Setup(browser => browser.CloseAsync(It.IsAny<BrowserCloseOptions>()))
            .Returns(Task.CompletedTask);

        var session = new BrowserSession(
            mockPlaywright.Object,
            mockBrowser.Object,
            mockContext.Object,
            options,
            new DiagnosticsService());

        return new PageWrapper(mockPage.Object, session, new DiagnosticsService());
    }
}