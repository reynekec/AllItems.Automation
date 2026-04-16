using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using AllItems.Automation.Browser.App.Credentials.Models;
using AllItems.Automation.Browser.App.Models;
using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.Services.Credentials;
using AllItems.Automation.Browser.App.Services.Flow;
using AllItems.Automation.Browser.Core.Abstractions;
using AllItems.Automation.Browser.Core.Browser;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AppBrowserType = AllItems.Automation.Browser.Core.Configuration.BrowserType;

namespace WpfAutomation.Core.Tests;

public sealed class FlowExecutionBridgeTests
{
    [Fact]
    public void FlowExecutionMapper_Preserves_ActionParameters_For_ActionNodes()
    {
        var editing = new FlowEditingService(new FlowActionParameterResolver());
        var document = editing.CreateEmptyDocument("flow");
        document = editing.AddActionNode(document, CreateRequest("open-browser"), 20, 20);

        var customized = new OpenBrowserActionParameters("firefox", false, 1234, 2);
        document = document.ReplaceActionParameters(document.Selection.PrimaryNodeId!, customized);

        var graph = new FlowExecutionMapper().Map(document);
        var executionNode = graph.Nodes.Single(node => string.Equals(node.ActionId, "open-browser", StringComparison.Ordinal));

        executionNode.ActionParameters.Should().BeEquivalentTo(customized);
    }

    [Fact]
    public async Task PrepareRunAsync_Uses_OpenBrowser_Node_Headless_Setting()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var diagnostics = new DiagnosticsService();
        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 3 },
            diagnostics);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("firefox", false, 1234, 2),
                },
            ],
            Edges = [],
        };

        await bridge.PrepareRunAsync(graph);

        launcherFactory.RequestedBrowserType.Should().Be(AppBrowserType.Firefox);
        launcherFactory.LastOptions.Should().NotBeNull();
        launcherFactory.LastOptions!.Headless.Should().BeFalse();
        launcherFactory.LastOptions.TimeoutMs.Should().Be(1234);
        launcherFactory.LastOptions.RetryCount.Should().Be(2);
        diagnostics.GetLogs().Should().Contain(entry => entry.Message.Contains("Flow action start."));
        diagnostics.GetLogs().Should().Contain(entry => entry.Message.Contains("Flow action complete."));

        await bridge.CloseActiveSessionAsync();
    }

    [Fact]
    public async Task PrepareRunAsync_Logs_NodeOutcome_Pass_Marker()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var diagnostics = new DiagnosticsService();
        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            diagnostics);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
            ],
            Edges = [],
        };

        await bridge.PrepareRunAsync(graph);

        diagnostics.GetLogs().Should().Contain(entry => entry.Message.Contains("[RUN][NODE][PASS] Open browser (open-node)"));

        await bridge.CloseActiveSessionAsync();
    }

    [Fact]
    public async Task PrepareRunAsync_Uses_ActionSpecific_Timeouts_For_Navigation_And_Click()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            new DiagnosticsService());

        launcherFactory.PageMock
            .Setup(candidate => candidate.GotoAsync(
                "https://example.com/",
                It.Is<PageGotoOptions>(options =>
                    options.Timeout == 30000 &&
                    options.WaitUntil == WaitUntilState.NetworkIdle)))
            .ReturnsAsync(Mock.Of<IResponse>());
        launcherFactory.PageMock
            .Setup(candidate => candidate.TitleAsync())
            .ReturnsAsync("Example");
        launcherFactory.PageMock
            .SetupGet(candidate => candidate.Url)
            .Returns("https://example.com/");
        launcherFactory.PageMock
            .Setup(candidate => candidate.Locator(
                "#cta",
                It.IsAny<PageLocatorOptions>()))
            .Returns(launcherFactory.LocatorMock.Object);
        launcherFactory.LocatorMock
            .Setup(candidate => candidate.ClickAsync(It.Is<LocatorClickOptions>(options => options.Timeout == 10000)))
            .Returns(Task.CompletedTask);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-navigate",
                    SourceNodeId = "navigate-node",
                    DisplayLabel = "Navigate",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "navigate-to-url",
                    ActionParameters = new NavigateToUrlActionParameters("https://example.com", 30000, true),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-click",
                    SourceNodeId = "click-node",
                    DisplayLabel = "Click",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "click-element",
                    ActionParameters = new ClickElementActionParameters("#cta", null, false, 10000),
                },
            ],
            Edges =
            [
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-navigate",
                },
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-navigate",
                    ToExecutionNodeId = "exec-click",
                },
            ],
        };

        await bridge.PrepareRunAsync(graph);

        launcherFactory.PageMock.VerifyAll();
        launcherFactory.LocatorMock.VerifyAll();

        await bridge.CloseActiveSessionAsync();
    }

    [Fact]
    public async Task PrepareRunAsync_ClickElement_RetriesWithInteractiveIdSelector_OnStrictModeViolation()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var diagnostics = new DiagnosticsService();
        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            diagnostics);

        const string idSelector = "[id=\"continue\"]";
        const string interactiveSelector = "input[id=\"continue\"], button[id=\"continue\"], a[id=\"continue\"], [role=\"button\"][id=\"continue\"]";

        var strictLocator = new Mock<ILocator>(MockBehavior.Strict);
        strictLocator
            .Setup(candidate => candidate.ClickAsync(It.Is<LocatorClickOptions>(options => options.Timeout == 10000)))
            .ThrowsAsync(new Exception("strict mode violation: locator resolved to 2 elements"));
        strictLocator
            .Setup(candidate => candidate.ScreenshotAsync(It.IsAny<LocatorScreenshotOptions>()))
            .ReturnsAsync(Array.Empty<byte>());

        var interactiveLocator = new Mock<ILocator>(MockBehavior.Strict);
        interactiveLocator
            .Setup(candidate => candidate.ClickAsync(It.Is<LocatorClickOptions>(options => options.Timeout == 10000)))
            .Returns(Task.CompletedTask);

        launcherFactory.PageMock
            .Setup(candidate => candidate.Locator(idSelector, It.IsAny<PageLocatorOptions>()))
            .Returns(strictLocator.Object);
        launcherFactory.PageMock
            .Setup(candidate => candidate.Locator(interactiveSelector, It.IsAny<PageLocatorOptions>()))
            .Returns(interactiveLocator.Object);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-click",
                    SourceNodeId = "click-node",
                    DisplayLabel = "Click",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "click-element",
                    ActionParameters = new ClickElementActionParameters(idSelector, null, false, 10000),
                },
            ],
            Edges =
            [
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-click",
                },
            ],
        };

        await bridge.PrepareRunAsync(graph);

        diagnostics.GetLogs().Should().Contain(log => log.Message.Contains("Click strict-mode fallback"));
        strictLocator.VerifyAll();
        interactiveLocator.VerifyAll();
        await bridge.CloseActiveSessionAsync();
    }

    [Fact]
    public async Task PrepareRunAsync_ClickElement_RetriesWithInteractiveIdSelector_ForIdShorthand_OnStrictModeViolation()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var diagnostics = new DiagnosticsService();
        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            diagnostics);

        const string idSelector = "id=continue";
        const string interactiveSelector = "input[id=\"continue\"], button[id=\"continue\"], a[id=\"continue\"], [role=\"button\"][id=\"continue\"]";

        var strictLocator = new Mock<ILocator>(MockBehavior.Strict);
        strictLocator
            .Setup(candidate => candidate.ClickAsync(It.Is<LocatorClickOptions>(options => options.Timeout == 10000)))
            .ThrowsAsync(new Exception("locator.click: strict mode violation"));
        strictLocator
            .Setup(candidate => candidate.ScreenshotAsync(It.IsAny<LocatorScreenshotOptions>()))
            .ReturnsAsync(Array.Empty<byte>());

        var interactiveLocator = new Mock<ILocator>(MockBehavior.Strict);
        interactiveLocator
            .Setup(candidate => candidate.ClickAsync(It.Is<LocatorClickOptions>(options => options.Timeout == 10000)))
            .Returns(Task.CompletedTask);

        launcherFactory.PageMock
            .Setup(candidate => candidate.Locator(idSelector, It.IsAny<PageLocatorOptions>()))
            .Returns(strictLocator.Object);
        launcherFactory.PageMock
            .Setup(candidate => candidate.Locator(interactiveSelector, It.IsAny<PageLocatorOptions>()))
            .Returns(interactiveLocator.Object);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-click",
                    SourceNodeId = "click-node",
                    DisplayLabel = "Click",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "click-element",
                    ActionParameters = new ClickElementActionParameters(idSelector, null, false, 10000),
                },
            ],
            Edges =
            [
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-click",
                },
            ],
        };

        await bridge.PrepareRunAsync(graph);

        diagnostics.GetLogs().Should().Contain(log => log.Message.Contains("Click strict-mode fallback"));
        strictLocator.VerifyAll();
        interactiveLocator.VerifyAll();
        await bridge.CloseActiveSessionAsync();
    }

    [Fact]
    public async Task PrepareRunAsync_NavigateWithCredential_ResolvesCredential_And_Executes_WebAuth()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var credentialStore = new Mock<ICredentialStore>(MockBehavior.Strict);
        var webAuthExecutor = new Mock<IWebAuthExecutor>(MockBehavior.Strict);
        var credentialId = Guid.Parse("3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042");

        var credential = new WebCredentialEntry(
            credentialId,
            "Contoso Login",
            WebAuthKind.UsernamePassword,
            new Dictionary<string, string>
            {
                [WebCredentialEntry.FieldKeys.Username] = "alice",
                [WebCredentialEntry.FieldKeys.Password] = "secret",
            });

        launcherFactory.PageMock
            .Setup(candidate => candidate.GotoAsync(
                "https://example.com/",
                It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(Mock.Of<IResponse>());
        launcherFactory.PageMock
            .Setup(candidate => candidate.TitleAsync())
            .ReturnsAsync("Example");
        launcherFactory.PageMock
            .SetupGet(candidate => candidate.Url)
            .Returns("https://example.com/");

        credentialStore
            .Setup(store => store.GetByIdAsync(credentialId))
            .ReturnsAsync(credential);

        webAuthExecutor
            .Setup(executor => executor.ExecuteAsync(
                It.IsAny<IPageWrapper>(),
                It.IsAny<BrowserSession>(),
                It.Is<WebCredentialEntry>(entry => entry.Id == credentialId),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            new DiagnosticsService(),
            masterPasswordService: null,
            credentialStore: credentialStore.Object,
            webAuthExecutor: webAuthExecutor.Object);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-navigate",
                    SourceNodeId = "navigate-node",
                    DisplayLabel = "Navigate",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "navigate-to-url",
                    ActionParameters = new NavigateToUrlActionParameters(
                        "https://example.com",
                        30000,
                        true,
                        credentialId.ToString(),
                        "Contoso Login",
                        true),
                },
            ],
            Edges =
            [
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-navigate",
                },
            ],
        };

        await bridge.PrepareRunAsync(graph);

        credentialStore.VerifyAll();
        webAuthExecutor.VerifyAll();
        await bridge.CloseActiveSessionAsync();
    }

    [Fact]
    public async Task PrepareRunAsync_NavigateWithMissingCredential_Throws()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var credentialStore = new Mock<ICredentialStore>(MockBehavior.Strict);
        var credentialId = Guid.Parse("3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042");

        launcherFactory.PageMock
            .Setup(candidate => candidate.GotoAsync(
                "https://example.com/",
                It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(Mock.Of<IResponse>());
        launcherFactory.PageMock
            .Setup(candidate => candidate.TitleAsync())
            .ReturnsAsync("Example");
        launcherFactory.PageMock
            .SetupGet(candidate => candidate.Url)
            .Returns("https://example.com/");

        credentialStore
            .Setup(store => store.GetByIdAsync(credentialId))
            .ReturnsAsync((CredentialEntry?)null);

        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            new DiagnosticsService(),
            masterPasswordService: null,
            credentialStore: credentialStore.Object,
            webAuthExecutor: null);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-navigate",
                    SourceNodeId = "navigate-node",
                    DisplayLabel = "Navigate",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "navigate-to-url",
                    ActionParameters = new NavigateToUrlActionParameters(
                        "https://example.com",
                        30000,
                        true,
                        credentialId.ToString(),
                        "Contoso Login",
                        true),
                },
            ],
            Edges =
            [
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-navigate",
                },
            ],
        };

        var act = async () => await bridge.PrepareRunAsync(graph);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    public async Task PrepareRunAsync_NavigateWithHttpBasicAuth_Configures_ContextHttpCredentials_Before_Navigation()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var credentialStore = new Mock<ICredentialStore>(MockBehavior.Strict);
        var webAuthExecutor = new Mock<IWebAuthExecutor>(MockBehavior.Strict);
        var credentialId = Guid.Parse("fc2f9a02-bae7-4d8f-a7c7-2b90980ed463");

        launcherFactory.PageMock
            .Setup(candidate => candidate.GotoAsync(
                "https://example.com/",
                It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(Mock.Of<IResponse>());
        launcherFactory.PageMock
            .Setup(candidate => candidate.TitleAsync())
            .ReturnsAsync("Example");
        launcherFactory.PageMock
            .SetupGet(candidate => candidate.Url)
            .Returns("https://example.com/");

        credentialStore
            .Setup(store => store.GetByIdAsync(credentialId))
            .ReturnsAsync(new WebCredentialEntry(
                credentialId,
                "Basic Login",
                WebAuthKind.HttpBasicAuth,
                new Dictionary<string, string>
                {
                    [WebCredentialEntry.FieldKeys.Username] = "alice",
                    [WebCredentialEntry.FieldKeys.Password] = "secret",
                }));

        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            new DiagnosticsService(),
            masterPasswordService: null,
            credentialStore: credentialStore.Object,
            webAuthExecutor: webAuthExecutor.Object);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-navigate",
                    SourceNodeId = "navigate-node",
                    DisplayLabel = "Navigate",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "navigate-to-url",
                    ActionParameters = new NavigateToUrlActionParameters(
                        "https://example.com",
                        30000,
                        true,
                        credentialId.ToString(),
                        "Basic Login",
                        true),
                },
            ],
            Edges =
            [
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-navigate",
                },
            ],
        };

        await bridge.PrepareRunAsync(graph);

        launcherFactory.LastOptions.Should().NotBeNull();
        launcherFactory.LastOptions!.HttpCredentials.Should().NotBeNull();
        launcherFactory.LastOptions.HttpCredentials!.Username.Should().Be("alice");
        launcherFactory.LastOptions.HttpCredentials.Password.Should().Be("secret");
        webAuthExecutor.Verify(
            executor => executor.ExecuteAsync(
                It.IsAny<IPageWrapper>(),
                It.IsAny<BrowserSession>(),
                It.IsAny<WebCredentialEntry>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        await bridge.CloseActiveSessionAsync();
    }

    [Fact]
    public async Task PrepareRunAsync_NavigateWithMtls_Configures_ContextClientCertificate_Before_Navigation()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var credentialStore = new Mock<ICredentialStore>(MockBehavior.Strict);
        var webAuthExecutor = new Mock<IWebAuthExecutor>(MockBehavior.Strict);
        var credentialId = Guid.Parse("914d1a41-a5b0-4f15-a81a-45231b55f803");

        launcherFactory.PageMock
            .Setup(candidate => candidate.GotoAsync(
                "https://example.com/",
                It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(Mock.Of<IResponse>());
        launcherFactory.PageMock
            .Setup(candidate => candidate.TitleAsync())
            .ReturnsAsync("Example");
        launcherFactory.PageMock
            .SetupGet(candidate => candidate.Url)
            .Returns("https://example.com/");

        credentialStore
            .Setup(store => store.GetByIdAsync(credentialId))
            .ReturnsAsync(new WebCredentialEntry(
                credentialId,
                "mTLS",
                WebAuthKind.CertificateMtls,
                new Dictionary<string, string>
                {
                    [WebCredentialEntry.FieldKeys.CertificatePath] = "cert.pem",
                    [WebCredentialEntry.FieldKeys.PrivateKeyPath] = "key.pem",
                    [WebCredentialEntry.FieldKeys.CertificatePassword] = "passphrase",
                }));

        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            new DiagnosticsService(),
            masterPasswordService: null,
            credentialStore: credentialStore.Object,
            webAuthExecutor: webAuthExecutor.Object);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-navigate",
                    SourceNodeId = "navigate-node",
                    DisplayLabel = "Navigate",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "navigate-to-url",
                    ActionParameters = new NavigateToUrlActionParameters(
                        "https://example.com",
                        30000,
                        true,
                        credentialId.ToString(),
                        "mTLS",
                        true),
                },
            ],
            Edges =
            [
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-navigate",
                },
            ],
        };

        await bridge.PrepareRunAsync(graph);

        launcherFactory.LastOptions.Should().NotBeNull();
        launcherFactory.LastOptions!.ClientCertificates.Should().NotBeNull();
        launcherFactory.LastOptions.ClientCertificates!.Should().HaveCount(1);
        launcherFactory.LastOptions.ClientCertificates[0].Origin.Should().Be("https://example.com");
        launcherFactory.LastOptions.ClientCertificates[0].CertPath.Should().Be("cert.pem");
        launcherFactory.LastOptions.ClientCertificates[0].KeyPath.Should().Be("key.pem");
        launcherFactory.LastOptions.ClientCertificates[0].Passphrase.Should().Be("passphrase");
        webAuthExecutor.Verify(
            executor => executor.ExecuteAsync(
                It.IsAny<IPageWrapper>(),
                It.IsAny<BrowserSession>(),
                It.IsAny<WebCredentialEntry>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        await bridge.CloseActiveSessionAsync();
    }

    [Fact]
    public async Task PrepareRunAsync_WaitForUserConfirmation_Continues_When_UserConfirms()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var confirmationService = new RecordingUserConfirmationDialogService(result: true);

        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            new DiagnosticsService(),
            userConfirmationDialogService: confirmationService);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-confirm",
                    SourceNodeId = "confirm-node",
                    DisplayLabel = "Wait for user confirmation",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "wait-for-user-confirmation",
                    ActionParameters = new WaitForUserConfirmationActionParameters("Please confirm", "Continue this flow?"),
                },
            ],
            Edges = [],
        };

        await bridge.PrepareRunAsync(graph);

        confirmationService.CallCount.Should().Be(1);
        confirmationService.LastTitle.Should().Be("Please confirm");
        confirmationService.LastMessage.Should().Be("Continue this flow?");
    }

    [Fact]
    public async Task PrepareRunAsync_WaitForUserConfirmation_Cancels_When_UserDeclines()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var confirmationService = new RecordingUserConfirmationDialogService(result: false);

        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            new DiagnosticsService(),
            userConfirmationDialogService: confirmationService);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-confirm",
                    SourceNodeId = "confirm-node",
                    DisplayLabel = "Wait for user confirmation",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "wait-for-user-confirmation",
                    ActionParameters = new WaitForUserConfirmationActionParameters(),
                },
            ],
            Edges = [],
        };

        var act = async () => await bridge.PrepareRunAsync(graph);

        await act.Should().ThrowAsync<OperationCanceledException>();
        confirmationService.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task PrepareRunAsync_WaitForUrl_Pauses_Within_Polling_And_Resumes_Without_Deadlock()
    {
        var runtimeExecutor = new StubFlowRuntimeExecutor();
        var launcherFactory = new RecordingBrowserLauncherFactory();
        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 0 },
            new DiagnosticsService());

        var currentUrl = "https://example.com/start";
        launcherFactory.PageMock
            .SetupGet(candidate => candidate.Url)
            .Returns(() => currentUrl);

        var graph = new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-open",
                    SourceNodeId = "open-node",
                    DisplayLabel = "Open browser",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "open-browser",
                    ActionParameters = new OpenBrowserActionParameters("chromium", false, 5000, 0),
                },
                new ExecutionFlowNode
                {
                    ExecutionNodeId = "exec-wait-url",
                    SourceNodeId = "wait-url-node",
                    DisplayLabel = "Wait for URL",
                    NodeKind = FlowNodeKind.Action,
                    ActionId = "wait-for-url",
                    ActionParameters = new WaitForUrlActionParameters("https://example.com/done", 2000, false),
                },
            ],
            Edges =
            [
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-wait-url",
                },
            ],
        };

        using var runControl = new PauseAtCallExecutionControl(pauseAtCall: 3);
        var runTask = bridge.PrepareRunAsync(graph, runExecutionControl: runControl);

        await runControl.WaitUntilPausedAsync();
        runTask.IsCompleted.Should().BeFalse();

        currentUrl = "https://example.com/done";
        runControl.Resume();

        await runTask;
    }

    private static UiActionDragRequest CreateRequest(string actionId)
    {
        return new UiActionDragRequest
        {
            ActionId = actionId,
            ActionName = actionId,
            CategoryId = "browser",
            CategoryName = "Browser",
            IsContainer = false,
        };
    }

    private sealed class StubFlowRuntimeExecutor : IFlowRuntimeExecutor
    {
        public Task<FlowRuntimeExecutionResult> ExecuteAsync(
            ExecutionFlowGraph executionGraph,
            CancellationToken cancellationToken = default,
            IFlowRunExecutionControl? runExecutionControl = null)
        {
            return Task.FromResult(new FlowRuntimeExecutionResult(
                executionGraph.Nodes.Select(node => node.SourceNodeId).ToList(),
                new Dictionary<string, int>(StringComparer.Ordinal)));
        }
    }

    private sealed class PauseAtCallExecutionControl : IFlowRunExecutionControl
    {
        private readonly int _pauseAtCall;
        private readonly TaskCompletionSource<bool> _enteredPause = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _sync = new();
        private TaskCompletionSource<bool> _resumeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _waitCallCount;
        private bool _isPauseRequested;

        public PauseAtCallExecutionControl(int pauseAtCall)
        {
            _pauseAtCall = pauseAtCall;
        }

        public bool IsPauseRequested
        {
            get
            {
                lock (_sync)
                {
                    return _isPauseRequested;
                }
            }
        }

        public void RequestPause()
        {
            lock (_sync)
            {
                _isPauseRequested = true;
            }
        }

        public void Resume()
        {
            TaskCompletionSource<bool> signal;

            lock (_sync)
            {
                _isPauseRequested = false;
                signal = _resumeSignal;
                _resumeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            signal.TrySetResult(true);
        }

        public async Task WaitIfPausedAsync(CancellationToken cancellationToken = default)
        {
            Task waitTask;

            lock (_sync)
            {
                _waitCallCount++;
                if (_waitCallCount != _pauseAtCall && !_isPauseRequested)
                {
                    return;
                }

                _isPauseRequested = true;
                waitTask = _resumeSignal.Task;
                _enteredPause.TrySetResult(true);
            }

            await waitTask.WaitAsync(cancellationToken);
        }

        public Task WaitUntilPausedAsync()
        {
            return _enteredPause.Task;
        }

        public void Dispose()
        {
            Resume();
        }
    }

    private sealed class RecordingBrowserLauncherFactory : IBrowserLauncherFactory
    {
        public AppBrowserType? RequestedBrowserType { get; private set; }

        public BrowserOptions? LastOptions { get; set; }

        public Mock<IPage> PageMock { get; } = new();

        public Mock<ILocator> LocatorMock { get; } = new();

        public IBrowserLauncher Create(AppBrowserType browserType)
        {
            RequestedBrowserType = browserType;
            return new RecordingBrowserLauncher(this);
        }
    }

    private sealed class RecordingUserConfirmationDialogService : IUserConfirmationDialogService
    {
        private readonly bool _result;

        public RecordingUserConfirmationDialogService(bool result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public string? LastTitle { get; private set; }

        public string? LastMessage { get; private set; }

        public Task<bool> WaitForConfirmationAsync(string title, string message, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTitle = title;
            LastMessage = message;
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingBrowserLauncher : IBrowserLauncher
    {
        private readonly RecordingBrowserLauncherFactory _factory;

        public RecordingBrowserLauncher(RecordingBrowserLauncherFactory factory)
        {
            _factory = factory;
        }

        public Task<IPageWrapper> NavigateUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<BrowserSession> StartAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
            var sessionOptions = new BrowserOptions
            {
                Headless = options.Headless,
                TimeoutMs = options.TimeoutMs,
                RetryCount = options.RetryCount,
                NavigationWaitUntilNetworkIdle = options.NavigationWaitUntilNetworkIdle,
                ScreenshotDirectory = options.ScreenshotDirectory,
                InspectionExportDirectory = options.InspectionExportDirectory,
                HttpCredentials = options.HttpCredentials,
                ClientCertificates = options.ClientCertificates,
                ExtraHttpHeaders = options.ExtraHttpHeaders,
            };
            _factory.LastOptions = sessionOptions;

            return Task.FromResult(CreateSession(sessionOptions));
        }

        private BrowserSession CreateSession(BrowserOptions options)
        {
            var playwright = new Mock<IPlaywright>();
            var browser = new Mock<IBrowser>();
            var context = new Mock<IBrowserContext>();

            context.Setup(candidate => candidate.NewPageAsync()).ReturnsAsync(_factory.PageMock.Object);
            context.Setup(candidate => candidate.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(Task.CompletedTask);
            browser.Setup(candidate => candidate.CloseAsync(It.IsAny<BrowserCloseOptions>())).Returns(Task.CompletedTask);

            return new BrowserSession(
                playwright.Object,
                browser.Object,
                context.Object,
                options,
                new DiagnosticsService());
        }
    }
}
