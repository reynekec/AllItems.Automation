using WpfAutomation.Core.Inspection;

namespace WpfAutomation.Core.Abstractions;

/// <summary>
/// Represents a wrapped Playwright page with fluent automation operations.
/// </summary>
public interface IPageWrapper
{
    /// <summary>Gets the current page URL.</summary>
    string CurrentUrl { get; }

    /// <summary>Gets the current page title when available.</summary>
    string? Title { get; }

    /// <summary>
    /// Navigates the current page to the supplied URL.
    /// </summary>
    Task<IPageWrapper> NavigateUrlAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a search context for locating UI elements.
    /// </summary>
    ISearchContext Search();

    /// <summary>
    /// Creates a page inspector for DOM/frame inspection.
    /// </summary>
    IPageInspector InspectPage();
}