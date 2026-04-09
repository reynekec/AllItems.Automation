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
        var bridge = new PlaywrightFlowExecutionBridge(
            runtimeExecutor,
            launcherFactory,
            new BrowserOptions { Headless = true, TimeoutMs = 5000, RetryCount = 3 },
            new DiagnosticsService());

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
            _factory.LastOptions = new BrowserOptions
            {
                Headless = options.Headless,
                TimeoutMs = options.TimeoutMs,
                RetryCount = options.RetryCount,
                ScreenshotDirectory = options.ScreenshotDirectory,
                InspectionExportDirectory = options.InspectionExportDirectory,
            };

            return Task.FromResult(CreateSession());
        }

        private static BrowserSession CreateSession()
        {
            var playwright = new Mock<IPlaywright>();
            var browser = new Mock<IBrowser>();
            var context = new Mock<IBrowserContext>();
            var page = new Mock<IPage>();

            context.Setup(candidate => candidate.NewPageAsync()).ReturnsAsync(page.Object);
            context.Setup(candidate => candidate.CloseAsync(It.IsAny<BrowserContextCloseOptions>())).Returns(Task.CompletedTask);
            browser.Setup(candidate => candidate.CloseAsync(It.IsAny<BrowserCloseOptions>())).Returns(Task.CompletedTask);

            return new BrowserSession(
                playwright.Object,
                browser.Object,
                context.Object,
                new BrowserOptions(),
                new DiagnosticsService());
        }
    }
}
