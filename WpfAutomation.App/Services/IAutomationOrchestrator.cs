using WpfAutomation.Core.Abstractions;

namespace WpfAutomation.App.Services;

public interface IAutomationOrchestrator
{
    Task<IPageWrapper> RunNavigationAsync(string url, CancellationToken cancellationToken);

    Task CloseActiveSessionAsync();
}
