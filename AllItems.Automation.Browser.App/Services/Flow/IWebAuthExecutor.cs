using AllItems.Automation.Browser.App.Credentials.Models;
using AllItems.Automation.Browser.Core.Abstractions;
using AllItems.Automation.Browser.Core.Browser;

namespace AllItems.Automation.Browser.App.Services.Flow;

public interface IWebAuthExecutor
{
    Task ExecuteAsync(
        IPageWrapper page,
        BrowserSession session,
        WebCredentialEntry credential,
        CancellationToken cancellationToken = default);
}
