using FluentAssertions;
using Microsoft.Playwright;
using Moq;
using WpfAutomation.App.Models;
using WpfAutomation.App.Models.Flow;
using WpfAutomation.App.Services.Flow;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;
using AppBrowserType = WpfAutomation.Core.Configuration.BrowserType;

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
        public Task<FlowRuntimeExecutionResult> ExecuteAsync(ExecutionFlowGraph executionGraph, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FlowRuntimeExecutionResult(
                executionGraph.Nodes.Select(node => node.SourceNodeId).ToList(),
                new Dictionary<string, int>(StringComparer.Ordinal)));
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
