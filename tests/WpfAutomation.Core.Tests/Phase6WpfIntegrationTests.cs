using FluentAssertions;
using WpfAutomation.App.Models;
using WpfAutomation.App.Services;
using WpfAutomation.App.Services.Flow;
using WpfAutomation.App.ViewModels;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.Core.Tests;

public sealed class Phase6WpfIntegrationTests
{
    [Fact]
    public async Task StartCommand_Executes_And_Updates_Status()
    {
        var orchestrator = new FakeAutomationOrchestrator();
        var diagnostics = new DiagnosticsService();
        var viewModel = new MainViewModel(orchestrator, new FakeActionCatalogBuilder(), diagnostics, new ImmediateUiDispatcherService());
        viewModel.Url = "https://example.com";

        viewModel.StartCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsRunning == false);

        orchestrator.RunCalls.Should().Be(1);
        viewModel.RunState.Should().Be(UiRunState.Completed);
        viewModel.Status.Should().Be("Completed");
        viewModel.StatusBarItems.Should().ContainSingle(item => item.Id == "run-state-text" && item.Text == "Completed");
    }

    [Fact]
    public async Task Diagnostics_Are_Bound_To_Ui_Logs()
    {
        var orchestrator = new FakeAutomationOrchestrator();
        var diagnostics = new DiagnosticsService();
        var viewModel = new MainViewModel(orchestrator, new FakeActionCatalogBuilder(), diagnostics, new ImmediateUiDispatcherService());

        diagnostics.Info("bound message");
        await WaitForAsync(() => viewModel.Logs.Any(log => log.Message.Contains("bound message")));

        viewModel.Logs.Should().Contain(log => log.Message.Contains("bound message"));
    }

    [Fact]
    public async Task StartCommand_Writes_RunLifecycle_Logs_To_Ui()
    {
        var orchestrator = new FakeAutomationOrchestrator();
        var diagnostics = new DiagnosticsService();
        var viewModel = new MainViewModel(orchestrator, new FakeActionCatalogBuilder(), diagnostics, new ImmediateUiDispatcherService());
        viewModel.Url = "https://example.com";

        viewModel.StartCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsRunning == false);

        viewModel.Logs.Should().Contain(log => log.Message.Contains("Run started."));
        viewModel.Logs.Should().Contain(log => log.Message.Contains("Run completed."));
    }

    [Fact]
    public async Task StopCommand_Cancels_Execution_From_Ui()
    {
        var orchestrator = new FakeAutomationOrchestrator
        {
            DelayMs = 1000,
        };

        var diagnostics = new DiagnosticsService();
        var viewModel = new MainViewModel(orchestrator, new FakeActionCatalogBuilder(), diagnostics, new ImmediateUiDispatcherService());
        viewModel.Url = "https://cancel.example";

        viewModel.StartCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsRunning);

        viewModel.StopCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsRunning == false);

        viewModel.RunState.Should().Be(UiRunState.Cancelled);
        viewModel.Status.Should().Be("Cancelled");
        orchestrator.CloseCalls.Should().BeGreaterThan(0);
        viewModel.StatusBarItems.Should().ContainSingle(item => item.Id == "run-state-text" && item.Text == "Cancelled");
    }

    [Fact]
    public async Task Status_Transitions_During_Execution()
    {
        var orchestrator = new FakeAutomationOrchestrator
        {
            DelayMs = 150,
        };

        var diagnostics = new DiagnosticsService();
        var viewModel = new MainViewModel(orchestrator, new FakeActionCatalogBuilder(), diagnostics, new ImmediateUiDispatcherService());
        viewModel.Url = "https://state.example";

        viewModel.StartCommand.Execute(null);
        await WaitForAsync(() => viewModel.Status == "Running");
        viewModel.Status.Should().Be("Running");
        viewModel.StatusBarItems
            .Should()
            .ContainSingle(item => item.Id == "stop-run-action" && item.IsEnabled);

        await WaitForAsync(() => viewModel.Status == "Completed");
        viewModel.Status.Should().Be("Completed");
        viewModel.StatusBarItems
            .Should()
            .ContainSingle(item => item.Id == "stop-run-action" && item.IsEnabled == false);
    }

    [Fact]
    public async Task StartCommand_UsesCanvasFlow_WhenNodesExist()
    {
        var orchestrator = new FakeAutomationOrchestrator();
        var diagnostics = new DiagnosticsService();
        var flowCanvas = FlowCanvasViewModel.CreateDefault(diagnostics);
        flowCanvas.HandleDrop(CreateRequest("open-browser"), new System.Windows.Point(20, 20));
        var flowBridge = new FakeFlowExecutionBridge();
        var viewModel = new MainViewModel(
            orchestrator,
            new FakeActionCatalogBuilder(),
            diagnostics,
            new ImmediateUiDispatcherService(),
            new BrowserOptions(),
            new FakeTestDockWindowService(),
            flowCanvas,
            dockLayoutPersistenceService: null,
            flowBridge);

        viewModel.Url = string.Empty;

        viewModel.StartCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsRunning == false);

        flowBridge.PrepareCalls.Should().Be(1);
        orchestrator.RunCalls.Should().Be(0);
        viewModel.RunState.Should().Be(UiRunState.Completed);
        viewModel.Status.Should().Be("Completed");
    }

    [Fact]
    public void Canvas_DocumentWindow_Can_Hide_Its_Tab_Header()
    {
        var viewModel = new MainViewModel(new FakeAutomationOrchestrator(), new FakeActionCatalogBuilder(), new DiagnosticsService(), new ImmediateUiDispatcherService());

        viewModel.Panels.Should().ContainSingle(panel =>
            panel.PanelId == "canvas"
            && panel.PanelKind == WpfAutomation.App.Docking.Models.DockPanelKind.DocumentWindow
            && panel.ShowTabHeader == false);
    }

    [Fact]
    public void Properties_Panel_Reuses_Runner_Slot_And_Removes_Legacy_Properties_Panel()
    {
        var viewModel = new MainViewModel(new FakeAutomationOrchestrator(), new FakeActionCatalogBuilder(), new DiagnosticsService(), new ImmediateUiDispatcherService());

        viewModel.Panels.Should().ContainSingle(panel =>
            panel.PanelId == "runner-controls"
            && panel.Title == "Properties"
            && panel.ContentKey == "Properties");
        viewModel.Panels.Should().NotContain(panel => panel.PanelId == "properties");
    }

    [Fact]
    public void StartDragCommand_Preserves_ContainerCapability_InRequest()
    {
        var viewModel = new MainViewModel(new FakeAutomationOrchestrator(), new FakeActionCatalogBuilder(), new DiagnosticsService(), new ImmediateUiDispatcherService());
        var request = new UiActionDragRequest
        {
            ActionId = "group-like",
            ActionName = "Group-like",
            CategoryId = "containers",
            CategoryName = "Containers",
            IsContainer = true,
        };

        viewModel.StartDragCommand.Execute(request);

        viewModel.LastDragActionRequest.Should().NotBeNull();
        viewModel.LastDragActionRequest!.IsContainer.Should().BeTrue();
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        condition().Should().BeTrue("condition should be met before timeout");
    }

    private sealed class ImmediateUiDispatcherService : IUiDispatcherService
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeActionCatalogBuilder : IActionCatalogBuilder
    {
        public IReadOnlyList<UiActionCategory> Build(IEnumerable<System.Reflection.Assembly> assemblies)
        {
            return [];
        }
    }

    private sealed class FakeTestDockWindowService : ITestDockWindowService
    {
        public void Show()
        {
        }
    }

    private sealed class FakeAutomationOrchestrator : IAutomationOrchestrator
    {
        public int RunCalls { get; private set; }

        public int CloseCalls { get; private set; }

        public int DelayMs { get; set; }

        public async Task<IPageWrapper> RunNavigationAsync(string url, CancellationToken cancellationToken)
        {
            RunCalls++;

            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return null!;
        }

        public Task CloseActiveSessionAsync()
        {
            CloseCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFlowExecutionBridge : IFlowExecutionBridge
    {
        public int PrepareCalls { get; private set; }

        public int CloseCalls { get; private set; }

        public Task PrepareRunAsync(ExecutionFlowGraph executionGraph, bool forceHeaded = false, CancellationToken cancellationToken = default)
        {
            PrepareCalls++;
            return Task.CompletedTask;
        }

        public Task CloseActiveSessionAsync()
        {
            CloseCalls++;
            return Task.CompletedTask;
        }
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
}
