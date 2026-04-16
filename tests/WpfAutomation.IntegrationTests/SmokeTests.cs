using FluentAssertions;
using AllItems.Automation.Browser.App.Models;
using AllItems.Automation.Browser.App.Services;
using AllItems.Automation.Browser.App.ViewModels;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;
using AllItems.Automation.Browser.Core.Exceptions;
using WpfAutomation.IntegrationTests.TestUtilities;

namespace WpfAutomation.IntegrationTests;

public sealed class SmokeTests
{
    [Fact]
    public void Basic_Wpf_Startup_Surface_Is_Constructible()
    {
        var diagnostics = new DiagnosticsService();
        var viewModel = new MainViewModel(
            new FakeOrchestrator(),
            new FakeActionCatalogBuilder(),
            diagnostics,
            new ImmediateDispatcher());

        viewModel.Should().NotBeNull();
        viewModel.Status.Should().Be("Ready");
    }

    [Fact]
    public async Task Basic_Automation_Flow_Works()
    {
        var (session, _) = await IntegrationHarness.TryStartSessionAsync(AllItems.Automation.Browser.Core.Configuration.BrowserType.Chromium);
        if (session is null)
        {
            return;
        }

        await using var disposable = session;
        var page = await session.NewPageAsync();
        await page.NavigateUrlAsync(IntegrationHarness.ToDataUrl("<html><body><button id='btn'>ok</button></body></html>"));

        var button = page.Search().ById("btn");
        await button.ClickAsync();
        (await button.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Launch_Cancellation_Is_Handled()
    {
        var diagnostics = new DiagnosticsService();
        var launcher = new AllItems.Automation.Browser.Core.Browser.BrowserLauncher(
            AllItems.Automation.Browser.Core.Configuration.BrowserType.Chromium,
            diagnosticsService: diagnostics);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = async () => await launcher.StartAsync(new BrowserOptions(), cts.Token);
        await action.Should().ThrowAsync<AutomationException>();
    }

    private sealed class ImmediateDispatcher : IUiDispatcherService
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOrchestrator : IAutomationOrchestrator
    {
        public Task<AllItems.Automation.Browser.Core.Abstractions.IPageWrapper> RunNavigationAsync(string url, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Fake orchestrator does not provide page navigation in this smoke test.");
        }

        public Task CloseActiveSessionAsync()
        {
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
}
