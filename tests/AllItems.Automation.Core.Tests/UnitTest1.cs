using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using AllItems.Automation.Browser.Core.Browser;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AllItems.Automation.Browser.Core.Exceptions;
using AllItems.Automation.Browser.Core.Page;
using AppBrowserType = AllItems.Automation.Browser.Core.Configuration.BrowserType;

namespace AllItems.Automation.Core.Tests;

public sealed class BrowserLifecycleTests
{
    [Theory]
    [InlineData(AppBrowserType.Chromium)]
    [InlineData(AppBrowserType.Firefox)]
    [InlineData(AppBrowserType.WebKit)]
    public async Task StartAsync_UsesRequestedBrowserType(AppBrowserType browserType)
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockChromium = new Mock<IBrowserType>();
        var mockFirefox = new Mock<IBrowserType>();
        var mockWebkit = new Mock<IBrowserType>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();

        mockPlaywright.SetupGet(playwright => playwright.Chromium).Returns(mockChromium.Object);
        mockPlaywright.SetupGet(playwright => playwright.Firefox).Returns(mockFirefox.Object);
        mockPlaywright.SetupGet(playwright => playwright.Webkit).Returns(mockWebkit.Object);

        mockChromium
            .Setup(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(mockBrowser.Object);
        mockFirefox
            .Setup(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(mockBrowser.Object);
        mockWebkit
            .Setup(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(mockBrowser.Object);

        mockBrowser
            .Setup(browser => browser.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(mockContext.Object);

        mockContext
            .Setup(context => context.CloseAsync(It.IsAny<BrowserContextCloseOptions>()))
            .Returns(Task.CompletedTask);
        mockBrowser
            .Setup(browser => browser.CloseAsync(It.IsAny<BrowserCloseOptions>()))
            .Returns(Task.CompletedTask);

        var launcher = new BrowserLauncher(browserType, () => Task.FromResult(mockPlaywright.Object), new DiagnosticsService());
        var session = await launcher.StartAsync(new BrowserOptions { Headless = true });

        session.Should().NotBeNull();

        mockChromium.Verify(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()), browserType == AppBrowserType.Chromium ? Times.Once() : Times.Never());
        mockFirefox.Verify(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()), browserType == AppBrowserType.Firefox ? Times.Once() : Times.Never());
        mockWebkit.Verify(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()), browserType == AppBrowserType.WebKit ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task StartAsync_ReturnsValidSession()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockChromium = new Mock<IBrowserType>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();

        mockPlaywright.SetupGet(playwright => playwright.Chromium).Returns(mockChromium.Object);
        mockChromium
            .Setup(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(mockBrowser.Object);
        mockBrowser
            .Setup(browser => browser.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(mockContext.Object);

        var launcher = new BrowserLauncher(AppBrowserType.Chromium, () => Task.FromResult(mockPlaywright.Object), new DiagnosticsService());

        var session = await launcher.StartAsync(new BrowserOptions());

        session.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_DisablesViewportEmulation_ForHeadedSessions()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockChromium = new Mock<IBrowserType>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();

        mockPlaywright.SetupGet(playwright => playwright.Chromium).Returns(mockChromium.Object);
        mockChromium
            .Setup(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(mockBrowser.Object);
        mockBrowser
            .Setup(browser => browser.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(mockContext.Object);

        var launcher = new BrowserLauncher(AppBrowserType.Chromium, () => Task.FromResult(mockPlaywright.Object), new DiagnosticsService());

        _ = await launcher.StartAsync(new BrowserOptions { Headless = false });

        mockBrowser.Verify(browser => browser.NewContextAsync(
            It.Is<BrowserNewContextOptions>(options =>
                options.ViewportSize != null &&
                options.ViewportSize.Width == -1 &&
                options.ViewportSize.Height == -1)),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_KeepsDefaultViewportEmulation_ForHeadlessSessions()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockChromium = new Mock<IBrowserType>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();

        mockPlaywright.SetupGet(playwright => playwright.Chromium).Returns(mockChromium.Object);
        mockChromium
            .Setup(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(mockBrowser.Object);
        mockBrowser
            .Setup(browser => browser.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(mockContext.Object);

        var launcher = new BrowserLauncher(AppBrowserType.Chromium, () => Task.FromResult(mockPlaywright.Object), new DiagnosticsService());

        _ = await launcher.StartAsync(new BrowserOptions { Headless = true });

        mockBrowser.Verify(browser => browser.NewContextAsync(
            It.Is<BrowserNewContextOptions>(options => options.ViewportSize == null)),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_Applies_ContextCredentials_And_ClientCertificates()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockChromium = new Mock<IBrowserType>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();

        mockPlaywright.SetupGet(playwright => playwright.Chromium).Returns(mockChromium.Object);
        mockChromium
            .Setup(browser => browser.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(mockBrowser.Object);
        mockBrowser
            .Setup(browser => browser.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(mockContext.Object);

        var credentials = new HttpCredentials
        {
            Username = "alice",
            Password = "secret",
        };
        var certificates = new[]
        {
            new ClientCertificate
            {
                Origin = "https://example.com",
                CertPath = "cert.pem",
                KeyPath = "key.pem",
                Passphrase = "pass",
            },
        };

        var launcher = new BrowserLauncher(AppBrowserType.Chromium, () => Task.FromResult(mockPlaywright.Object), new DiagnosticsService());

        _ = await launcher.StartAsync(new BrowserOptions
        {
            Headless = true,
            HttpCredentials = credentials,
            ClientCertificates = certificates,
        });

        mockBrowser.Verify(browser => browser.NewContextAsync(
            It.Is<BrowserNewContextOptions>(options =>
                options.HttpCredentials == credentials &&
                options.ClientCertificates != null &&
                options.ClientCertificates.Count() == 1 &&
                options.ClientCertificates.First().Origin == "https://example.com" &&
                options.ClientCertificates.First().CertPath == "cert.pem" &&
                options.ClientCertificates.First().KeyPath == "key.pem" &&
                options.ClientCertificates.First().Passphrase == "pass")),
            Times.Once);
    }

    [Fact]
    public async Task NewPageAsync_CreatesWrappedPage()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();

        mockContext.Setup(context => context.NewPageAsync()).ReturnsAsync(mockPage.Object);
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
            new BrowserOptions(),
            new DiagnosticsService());

        var page = await session.NewPageAsync();

        page.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPagesAsync_ReturnsOpenPages()
    {
        var mockPlaywright = new Mock<IPlaywright>();
        var mockBrowser = new Mock<IBrowser>();
        var mockContext = new Mock<IBrowserContext>();
        var mockPage = new Mock<IPage>();

        mockContext.SetupGet(context => context.Pages).Returns([mockPage.Object]);
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
            new BrowserOptions(),
            new DiagnosticsService());

        var pages = await session.GetPagesAsync();

        pages.Should().HaveCount(1);
    }

    [Fact]
    public async Task CloseAsync_IsSafeAndIdempotent()
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
            new BrowserOptions(),
            new DiagnosticsService());

        await session.CloseAsync();
        await session.CloseAsync();

        mockContext.Verify(context => context.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.Once());
        mockBrowser.Verify(browser => browser.CloseAsync(It.IsAny<BrowserCloseOptions>()), Times.Once());
        mockPlaywright.Verify(playwright => playwright.Dispose(), Times.Once());
    }
}

public sealed class PageNavigationTests
{
    [Fact]
    public async Task NavigateUrlAsync_NavigatesToValidUrl()
    {
        var mockPage = new Mock<IPage>();
        var session = CreateSessionWithPage(mockPage);
        var wrapper = new PageWrapper(mockPage.Object, session, new DiagnosticsService());

        mockPage
            .Setup(page => page.GotoAsync("https://example.com/", It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(Mock.Of<IResponse>());
        mockPage
            .Setup(page => page.TitleAsync())
            .ReturnsAsync("Example Domain");
        mockPage
            .SetupGet(page => page.Url)
            .Returns("https://example.com/");

        var returned = await wrapper.NavigateUrlAsync("https://example.com");

        returned.Should().Be(wrapper);
        wrapper.CurrentUrl.Should().Be("https://example.com/");
        wrapper.Title.Should().Be("Example Domain");
    }

    [Fact]
    public async Task NavigateUrlAsync_UsesNetworkIdleWaitState_WhenConfigured()
    {
        var mockPage = new Mock<IPage>();
        var session = CreateSessionWithPage(mockPage, new BrowserOptions
        {
            TimeoutMs = 4321,
            RetryCount = 0,
            NavigationWaitUntilNetworkIdle = true,
        });
        var wrapper = new PageWrapper(mockPage.Object, session, new DiagnosticsService());

        mockPage
            .Setup(page => page.GotoAsync(
                "https://example.com/",
                It.Is<PageGotoOptions>(options =>
                    options.Timeout == 4321 &&
                    options.WaitUntil == WaitUntilState.NetworkIdle)))
            .ReturnsAsync(Mock.Of<IResponse>());
        mockPage
            .Setup(page => page.TitleAsync())
            .ReturnsAsync("Example Domain");
        mockPage
            .SetupGet(page => page.Url)
            .Returns("https://example.com/");

        await wrapper.NavigateUrlAsync("https://example.com");
    }

    [Fact]
    public async Task NavigateUrlAsync_InvalidUrl_ThrowsNavigationException()
    {
        var mockPage = new Mock<IPage>();
        var session = CreateSessionWithPage(mockPage);
        var wrapper = new PageWrapper(mockPage.Object, session, new DiagnosticsService());

        var act = async () => await wrapper.NavigateUrlAsync("not-a-url");

        var exception = await act.Should().ThrowAsync<NavigationException>();
        exception.Which.Url.Should().Be("not-a-url");
    }

    [Fact]
    public async Task NavigateUrlAsync_AppliesRetryLogic()
    {
        var mockPage = new Mock<IPage>();
        var session = CreateSessionWithPage(mockPage, new BrowserOptions { RetryCount = 2, TimeoutMs = 5000 });
        var wrapper = new PageWrapper(mockPage.Object, session, new DiagnosticsService());

        var attempts = 0;
        mockPage
            .Setup(page => page.GotoAsync("https://retry.example/", It.IsAny<PageGotoOptions>()))
            .Returns(() =>
            {
                attempts++;
                return attempts < 3
                    ? throw new TimeoutException("transient")
                    : Task.FromResult<IResponse?>(Mock.Of<IResponse>());
            });

        mockPage
            .Setup(page => page.TitleAsync())
            .ReturnsAsync("Retry Success");
        mockPage
            .SetupGet(page => page.Url)
            .Returns("https://retry.example/");

        await wrapper.NavigateUrlAsync("https://retry.example");

        attempts.Should().Be(3);
    }

    [Fact]
    public async Task NavigateUrlAsync_OnFailure_CapturesScreenshotAndWrapsException()
    {
        var screenshotDirectory = Path.Combine(Path.GetTempPath(), $"wpf-automation-tests-{Guid.NewGuid():N}");
        var mockPage = new Mock<IPage>();
        var session = CreateSessionWithPage(
            mockPage,
            new BrowserOptions
            {
                RetryCount = 0,
                TimeoutMs = 5000,
                ScreenshotDirectory = screenshotDirectory,
            });
        var wrapper = new PageWrapper(mockPage.Object, session, new DiagnosticsService());

        mockPage
            .Setup(page => page.GotoAsync("https://fail.example/", It.IsAny<PageGotoOptions>()))
            .ThrowsAsync(new TimeoutException("timeout"));
        mockPage
            .Setup(page => page.ScreenshotAsync(It.IsAny<PageScreenshotOptions>()))
            .ReturnsAsync(Array.Empty<byte>());

        var act = async () => await wrapper.NavigateUrlAsync("https://fail.example");

        var exception = await act.Should().ThrowAsync<NavigationException>();
        exception.Which.ScreenshotPath.Should().NotBeNullOrWhiteSpace();
        mockPage.Verify(page => page.ScreenshotAsync(It.IsAny<PageScreenshotOptions>()), Times.Once);

        if (Directory.Exists(screenshotDirectory))
        {
            Directory.Delete(screenshotDirectory, true);
        }
    }

    [Fact]
    public async Task NavigateUrlAsync_WritesNavigationLogs()
    {
        var diagnosticsService = new DiagnosticsService();
        var mockPage = new Mock<IPage>();
        var session = CreateSessionWithPage(mockPage);
        var wrapper = new PageWrapper(mockPage.Object, session, diagnosticsService);

        mockPage
            .Setup(page => page.GotoAsync("https://logs.example/", It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(Mock.Of<IResponse>());
        mockPage
            .Setup(page => page.TitleAsync())
            .ReturnsAsync("Logs");
        mockPage
            .SetupGet(page => page.Url)
            .Returns("https://logs.example/");

        await wrapper.NavigateUrlAsync("https://logs.example");

        var logs = diagnosticsService.GetLogs();
        logs.Should().Contain(entry => entry.Message.Contains("Navigate start"));
        logs.Should().Contain(entry => entry.Message.Contains("Navigate complete"));
    }

    private static BrowserSession CreateSessionWithPage(Mock<IPage> mockPage, BrowserOptions? options = null)
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

        return new BrowserSession(
            mockPlaywright.Object,
            mockBrowser.Object,
            mockContext.Object,
            options ?? new BrowserOptions(),
            new DiagnosticsService());
    }
}
