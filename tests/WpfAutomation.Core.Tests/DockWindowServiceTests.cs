using FluentAssertions;
using System.Windows;
using WpfAutomation.App.Services;
using WpfAutomation.App.ViewModels;
using WpfAutomation.App.Commands;
using WpfAutomation.App.Models;
using WpfAutomation.Core.Diagnostics;
using WpfAutomation.Core.Abstractions;

namespace WpfAutomation.Core.Tests;

public sealed class DockWindowServiceTests
{
    [Fact]
    public void Show_CreatesSingleWindowAndReusesExistingInstance()
    {
        var factory = new FakeTestDockWindowFactory();
        var service = new TestDockWindowService(factory);

        service.Show();
        service.Show();

        factory.CreateCalls.Should().Be(1);
        factory.LastHandle!.ShowCalls.Should().Be(1);
        factory.LastHandle.ActivateCalls.Should().Be(2);
        factory.LastHandle.FocusCalls.Should().Be(1);
    }

    [Fact]
    public void Show_RestoresMinimizedWindowBeforeActivating()
    {
        var factory = new FakeTestDockWindowFactory();
        var service = new TestDockWindowService(factory);

        service.Show();
        factory.LastHandle!.WindowState = WindowState.Minimized;

        service.Show();

        factory.LastHandle.WindowState.Should().Be(WindowState.Normal);
    }

    [Fact]
    public void Show_AfterWindowClosed_CreatesNewInstance()
    {
        var factory = new FakeTestDockWindowFactory();
        var service = new TestDockWindowService(factory);

        service.Show();
        factory.LastHandle!.RaiseClosed();
        service.Show();

        factory.CreateCalls.Should().Be(2);
    }

    [Fact]
    public void OpenDockLabCommand_InvokesWindowService()
    {
        var dockWindowService = new RecordingTestDockWindowService();
        var viewModel = new MainViewModel(
            new FakeAutomationOrchestrator(),
            new FakeActionCatalogBuilder(),
            new DiagnosticsService(),
            new ImmediateUiDispatcherService(),
            dockWindowService);

        viewModel.OpenDockLabCommand.Execute(null);

        dockWindowService.ShowCalls.Should().Be(1);
    }

    private sealed class FakeTestDockWindowFactory : ITestDockWindowFactory
    {
        public int CreateCalls { get; private set; }

        public FakeWindowHandle? LastHandle { get; private set; }

        public ITestDockWindowHandle Create()
        {
            CreateCalls++;
            LastHandle = new FakeWindowHandle();
            return LastHandle;
        }
    }

    private sealed class FakeWindowHandle : ITestDockWindowHandle
    {
        public event EventHandler? Closed;

        public int ShowCalls { get; private set; }

        public int ActivateCalls { get; private set; }

        public int FocusCalls { get; private set; }

        public WindowState WindowState { get; set; }

        public void Show() => ShowCalls++;

        public void Activate() => ActivateCalls++;

        public void Focus() => FocusCalls++;

        public void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingTestDockWindowService : ITestDockWindowService
    {
        public int ShowCalls { get; private set; }

        public void Show() => ShowCalls++;
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

    private sealed class FakeAutomationOrchestrator : IAutomationOrchestrator
    {
        public Task<IPageWrapper> RunNavigationAsync(string url, CancellationToken cancellationToken) => Task.FromResult<IPageWrapper>(null!);

        public Task CloseActiveSessionAsync() => Task.CompletedTask;
    }
}
