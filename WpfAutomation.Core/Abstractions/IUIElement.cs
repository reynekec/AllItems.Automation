using WpfAutomation.Core.Inspection;
using WpfAutomation.Core.Reports;

namespace WpfAutomation.Core.Abstractions;

/// <summary>
/// Represents a located UI element and supported interactions.
/// </summary>
public interface IUIElement
{
    /// <summary>Clicks the element.</summary>
    Task ClickAsync(CancellationToken cancellationToken = default);

    /// <summary>Types text using keyboard-like input.</summary>
    Task TypeAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Fills the element with a value.</summary>
    Task FillAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Gets text content.</summary>
    Task<string> GetTextAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets an attribute value.</summary>
    Task<string?> GetAttributeAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Checks whether the element is visible.</summary>
    Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks whether the element is enabled.</summary>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>Moves the pointer over the element.</summary>
    Task HoverAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks the element when supported.</summary>
    Task CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>Unchecks the element when supported.</summary>
    Task UncheckAsync(CancellationToken cancellationToken = default);

    /// <summary>Selects an option value.</summary>
    Task SelectAsync(string value, CancellationToken cancellationToken = default);

    /// <summary>Waits until the element is visible.</summary>
    Task WaitForAsync(CancellationToken cancellationToken = default);

    /// <summary>Inspects the element and returns a structured report.</summary>
    Task<InspectionReport> InspectAsync(InspectOptions? options = null, CancellationToken cancellationToken = default);
}