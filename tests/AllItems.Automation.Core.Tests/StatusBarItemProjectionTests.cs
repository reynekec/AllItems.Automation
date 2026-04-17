using FluentAssertions;
using AllItems.Automation.Browser.App.Models;
using AllItems.Automation.Browser.App.Services;
using AllItems.Automation.Browser.App.ViewModels;
using AllItems.Automation.Browser.Core.Abstractions;
using AllItems.Automation.Browser.Core.Diagnostics;

namespace AllItems.Automation.Core.Tests;

public sealed class StatusBarItemProjectionTests
{
    [Fact]
    public void StatusBarItems_Are_Placed_And_Ordered_Predictably()
    {
        var viewModel = CreateViewModel();

        viewModel.StatusBarItems
            .Where(item => item.Placement == StatusBarItemPlacement.Left)
            .Select(item => item.Id)
            .Should()
            .ContainInOrder("run-state-text");

        viewModel.StatusBarItems
            .Where(item => item.Placement == StatusBarItemPlacement.Right)
            .OrderBy(item => item.Order)
            .Select(item => item.Id)
            .Should()
            .ContainInOrder("execution-profile", "diagnostics-count", "run-state-badge", "stop-run-action");
    }

    [Fact]
    public void Stop_Item_Enabled_State_Tracks_Run_State()
    {
        var orchestrator = new SlowAutomationOrchestrator();
        var diagnostics = new DiagnosticsService();
        var viewModel = new MainViewModel(orchestrator, new FakeActionCatalogBuilder(), diagnostics, new ImmediateUiDispatcherService());
        viewModel.Url = "https://example.com";

        viewModel.StatusBarItems.Should().ContainSingle(item => item.Id == "stop-run-action" && item.IsEnabled == false);

        viewModel.StartCommand.Execute(null);

        WaitFor(() => viewModel.IsRunning);
        viewModel.StatusBarItems.Should().ContainSingle(item => item.Id == "stop-run-action" && item.IsEnabled);

        viewModel.StopCommand.Execute(null);
        WaitFor(() => viewModel.IsRunning == false);
        viewModel.StatusBarItems.Should().ContainSingle(item => item.Id == "stop-run-action" && item.IsEnabled == false);
    }

    [Fact]
    public void StatusBarItems_Support_Icon_Text_And_Tooltip_Combinations()
    {
        var viewModel = CreateViewModel();
        var runStateItem = viewModel.StatusBarItems.Single(item => item.Id == "run-state-text");

        runStateItem.IconGlyph.Should().NotBeNullOrWhiteSpace();
        runStateItem.Text.Should().NotBeNullOrWhiteSpace();
        runStateItem.ToolTip.Should().NotBeNullOrWhiteSpace();

        runStateItem.IconGlyph = null;
        runStateItem.Text = "Ready";
        runStateItem.ToolTip = "Tooltip-only meaning for this item";

        runStateItem.IconGlyph.Should().BeNull();
        runStateItem.Text.Should().Be("Ready");
        runStateItem.ToolTip.Should().Be("Tooltip-only meaning for this item");
    }

    [Fact]
    public void StatusBarItems_Can_Represent_Disabled_And_NoAction_Cases()
    {
        var viewModel = CreateViewModel();
        var badge = viewModel.StatusBarItems.Single(item => item.Id == "run-state-badge");

        badge.Command.Should().BeNull();
        badge.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void StatusBarItems_Command_Can_Be_Invoked_From_Item()
    {
        var dockWindowService = new RecordingDockWindowService();
        var viewModel = new MainViewModel(
            new SlowAutomationOrchestrator(),
            new FakeActionCatalogBuilder(),
            new DiagnosticsService(),
            new ImmediateUiDispatcherService(),
            dockWindowService);

        viewModel.OpenDockLabCommand.Execute(null);

        dockWindowService.ShowCalls.Should().Be(1);
        viewModel.Status.Should().Be("Ready");
    }

    private static MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            new SlowAutomationOrchestrator(),
            new FakeActionCatalogBuilder(),
            new DiagnosticsService(),
            new ImmediateUiDispatcherService());
    }

    private static void WaitFor(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(20);
        }

        condition().Should().BeTrue();
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

    private sealed class SlowAutomationOrchestrator : IAutomationOrchestrator
    {
        public async Task<IPageWrapper> RunNavigationAsync(string url, CancellationToken cancellationToken)
        {
            await Task.Delay(350, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return null!;
        }

        public Task CloseActiveSessionAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDockWindowService : ITestDockWindowService
    {
        public int ShowCalls { get; private set; }

        public void Show()
        {
            ShowCalls++;
        }
    }
}