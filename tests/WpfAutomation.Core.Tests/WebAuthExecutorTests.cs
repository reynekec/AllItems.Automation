using FluentAssertions;
using Moq;
using WpfAutomation.App.Credentials.Models;
using WpfAutomation.App.Services;
using WpfAutomation.App.Services.Flow;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.Core.Tests;

public sealed class WebAuthExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_UsernamePassword_Fills_And_Submits_With_Default_Selectors()
    {
        var diagnostics = new DiagnosticsService();
        var dispatcher = new InlineDispatcherService();
        var executor = new WebAuthExecutor(diagnostics, dispatcher);

        var search = new Mock<ISearchContext>(MockBehavior.Strict);
        var page = new Mock<IPageWrapper>(MockBehavior.Strict);
        var usernameElement = new Mock<IUIElement>(MockBehavior.Strict);
        var passwordElement = new Mock<IUIElement>(MockBehavior.Strict);
        var submitElement = new Mock<IUIElement>(MockBehavior.Strict);
        var session = CreateSession();

        page.Setup(candidate => candidate.Search()).Returns(search.Object);

        search.Setup(candidate => candidate.ByCss("input[name='username']", It.IsAny<CancellationToken>()))
            .Returns(usernameElement.Object);
        usernameElement.Setup(candidate => candidate.FillAsync("alice", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        search.Setup(candidate => candidate.ByCss("input[type='password']", It.IsAny<CancellationToken>()))
            .Returns(passwordElement.Object);
        passwordElement.Setup(candidate => candidate.FillAsync("secret", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        search.Setup(candidate => candidate.ByCss("button[type='submit']", It.IsAny<CancellationToken>()))
            .Returns(submitElement.Object);
        submitElement.Setup(candidate => candidate.ClickAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var credential = new WebCredentialEntry(
            Guid.NewGuid(),
            "Portal",
            WebAuthKind.UsernamePassword,
            new Dictionary<string, string>
            {
                [WebCredentialEntry.FieldKeys.Username] = "alice",
                [WebCredentialEntry.FieldKeys.Password] = "secret",
            });

        await executor.ExecuteAsync(page.Object, session, credential);

        search.VerifyAll();
        usernameElement.VerifyAll();
        passwordElement.VerifyAll();
        submitElement.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_Custom_Logs_Warning_And_Skips_Page_Interaction()
    {
        var diagnostics = new DiagnosticsService();
        var executor = new WebAuthExecutor(diagnostics, new InlineDispatcherService());

        var page = new Mock<IPageWrapper>(MockBehavior.Strict);
        var session = CreateSession();

        var credential = new WebCredentialEntry(
            Guid.NewGuid(),
            "Informational",
            WebAuthKind.Custom,
            new Dictionary<string, string>());

        await executor.ExecuteAsync(page.Object, session, credential);

        diagnostics.GetLogs().Should().Contain(entry =>
            entry.Level == "WARN" &&
            entry.Message.Contains("informational only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_CertificateMtls_Logs_Info_And_Does_Not_Throw()
    {
        var diagnostics = new DiagnosticsService();
        var executor = new WebAuthExecutor(diagnostics, new InlineDispatcherService());

        var page = new Mock<IPageWrapper>(MockBehavior.Strict);
        var session = CreateSession();

        var credential = new WebCredentialEntry(
            Guid.NewGuid(),
            "Mtls",
            WebAuthKind.CertificateMtls,
            new Dictionary<string, string>
            {
                [WebCredentialEntry.FieldKeys.CertificatePath] = "cert.pem",
                [WebCredentialEntry.FieldKeys.CertificatePassword] = "pass",
            });

        await executor.ExecuteAsync(page.Object, session, credential);

        diagnostics.GetLogs().Should().Contain(entry =>
            entry.Level == "INFO" &&
            entry.Message.Contains("pre-navigation session bootstrap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ApiKeyBearer_Sets_Bearer_Authorization_Header()
    {
        var diagnostics = new DiagnosticsService();
        var executor = new WebAuthExecutor(diagnostics, new InlineDispatcherService());

        var page = new Mock<IPageWrapper>(MockBehavior.Strict);

        var context = new Mock<Microsoft.Playwright.IBrowserContext>(MockBehavior.Strict);
        context
            .Setup(candidate => candidate.SetExtraHTTPHeadersAsync(
                It.Is<IEnumerable<KeyValuePair<string, string>>>(headers =>
                    headers.Any(header =>
                        string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase) &&
                        header.Value == "Bearer tok-123"))))
            .Returns(Task.CompletedTask);

        var browser = new Mock<Microsoft.Playwright.IBrowser>(MockBehavior.Strict);

        var session = new BrowserSession(
            Mock.Of<Microsoft.Playwright.IPlaywright>(),
            browser.Object,
            context.Object,
            new BrowserOptions(),
            new DiagnosticsService());

        var credential = new WebCredentialEntry(
            Guid.NewGuid(),
            "Api",
            WebAuthKind.ApiKeyBearer,
            new Dictionary<string, string>
            {
                [WebCredentialEntry.FieldKeys.TokenName] = "API",
                [WebCredentialEntry.FieldKeys.Token] = "tok-123",
            });

        await executor.ExecuteAsync(page.Object, session, credential);

        context.Verify(candidate => candidate.SetExtraHTTPHeadersAsync(
            It.IsAny<IEnumerable<KeyValuePair<string, string>>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HttpBasicAuth_Sets_Basic_Authorization_Header()
    {
        var diagnostics = new DiagnosticsService();
        var executor = new WebAuthExecutor(diagnostics, new InlineDispatcherService());

        var page = new Mock<IPageWrapper>(MockBehavior.Strict);

        var expectedHeader = "Basic YWxpY2U6czNjcjN0";
        var context = new Mock<Microsoft.Playwright.IBrowserContext>(MockBehavior.Strict);
        context
            .Setup(candidate => candidate.SetExtraHTTPHeadersAsync(
                It.Is<IEnumerable<KeyValuePair<string, string>>>(headers =>
                    headers.Any(header =>
                        string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase) &&
                        header.Value == expectedHeader))))
            .Returns(Task.CompletedTask);

        var browser = new Mock<Microsoft.Playwright.IBrowser>(MockBehavior.Strict);

        var session = new BrowserSession(
            Mock.Of<Microsoft.Playwright.IPlaywright>(),
            browser.Object,
            context.Object,
            new BrowserOptions(),
            new DiagnosticsService());

        var credential = new WebCredentialEntry(
            Guid.NewGuid(),
            "Basic",
            WebAuthKind.HttpBasicAuth,
            new Dictionary<string, string>
            {
                [WebCredentialEntry.FieldKeys.Username] = "alice",
                [WebCredentialEntry.FieldKeys.Password] = "s3cr3t",
            });

        await executor.ExecuteAsync(page.Object, session, credential);

        context.Verify(candidate => candidate.SetExtraHTTPHeadersAsync(
            It.IsAny<IEnumerable<KeyValuePair<string, string>>>()), Times.Once);
    }

    private static BrowserSession CreateSession()
    {
        var playwright = new Mock<Microsoft.Playwright.IPlaywright>(MockBehavior.Strict);
        var browser = new Mock<Microsoft.Playwright.IBrowser>(MockBehavior.Strict);
        var context = new Mock<Microsoft.Playwright.IBrowserContext>(MockBehavior.Strict);

        context.Setup(candidate => candidate.CloseAsync(It.IsAny<Microsoft.Playwright.BrowserContextCloseOptions>()))
            .Returns(Task.CompletedTask);
        browser.Setup(candidate => candidate.CloseAsync(It.IsAny<Microsoft.Playwright.BrowserCloseOptions>()))
            .Returns(Task.CompletedTask);

        return new BrowserSession(
            playwright.Object,
            browser.Object,
            context.Object,
            new BrowserOptions(),
            new DiagnosticsService());
    }

    private sealed class InlineDispatcherService : IUiDispatcherService
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
