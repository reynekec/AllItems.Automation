using AllItems.Automation.Browser.Core.Browser;
using AllItems.Automation.Browser.Core.Configuration;

namespace AllItems.Automation.Browser.Core.Abstractions;

/// <summary>
/// Starts browser sessions and offers a convenience navigation entry point.
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>
    /// Launches a new session and navigates a new page to the supplied URL.
    /// </summary>
    /// <param name="url">Absolute URL to navigate to.</param>
    /// <param name="cancellationToken">Cancellation token for launch and navigation.</param>
    /// <returns>The wrapped page after successful navigation.</returns>
    Task<IPageWrapper> NavigateUrlAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a browser session with the provided options.
    /// </summary>
    /// <param name="options">Session-scoped browser options.</param>
    /// <param name="cancellationToken">Cancellation token for launch.</param>
    /// <returns>An active browser session.</returns>
    Task<BrowserSession> StartAsync(BrowserOptions options, CancellationToken cancellationToken = default);
}