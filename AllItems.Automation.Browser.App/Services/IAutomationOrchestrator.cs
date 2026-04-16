using AllItems.Automation.Browser.Core.Abstractions;

namespace AllItems.Automation.Browser.App.Services;

public interface IAutomationOrchestrator
{
    Task<IPageWrapper> RunNavigationAsync(string url, CancellationToken cancellationToken);

    Task CloseActiveSessionAsync();
}
