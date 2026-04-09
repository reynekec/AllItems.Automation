namespace WpfAutomation.Core.Abstractions;

/// <summary>
/// Provides fluent element lookup methods from the current page context.
/// </summary>
public interface ISearchContext
{
    /// <summary>Finds an element by HTML id.</summary>
    IUIElement ById(string id, CancellationToken cancellationToken = default);

    /// <summary>Finds an element by CSS selector.</summary>
    IUIElement ByCss(string selector, CancellationToken cancellationToken = default);

    /// <summary>Finds an element by ARIA role.</summary>
    IUIElement ByRole(string role, CancellationToken cancellationToken = default);

    /// <summary>Finds an element by visible text.</summary>
    IUIElement ByText(string text, CancellationToken cancellationToken = default);

    /// <summary>Finds an element by associated label text.</summary>
    IUIElement ByLabel(string label, CancellationToken cancellationToken = default);

    /// <summary>Finds an element by placeholder text.</summary>
    IUIElement ByPlaceholder(string text, CancellationToken cancellationToken = default);

    /// <summary>Finds an element by title attribute.</summary>
    IUIElement ByTitle(string title, CancellationToken cancellationToken = default);

    /// <summary>Finds an element by test id.</summary>
    IUIElement ByTestId(string testId, CancellationToken cancellationToken = default);
}