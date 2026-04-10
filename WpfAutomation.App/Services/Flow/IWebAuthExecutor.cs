using WpfAutomation.App.Credentials.Models;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;

namespace WpfAutomation.App.Services.Flow;

public interface IWebAuthExecutor
{
    Task ExecuteAsync(
        IPageWrapper page,
        BrowserSession session,
        WebCredentialEntry credential,
        CancellationToken cancellationToken = default);
}
