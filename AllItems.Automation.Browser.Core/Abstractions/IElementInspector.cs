using AllItems.Automation.Browser.Core.Inspection;
using AllItems.Automation.Browser.Core.Reports;

namespace AllItems.Automation.Browser.Core.Abstractions;

/// <summary>
/// Inspects a specific UI element and returns structured diagnostics.
/// </summary>
public interface IElementInspector
{
    /// <summary>
    /// Runs inspection for the specified element.
    /// </summary>
    Task<InspectionReport> InspectAsync(IUIElement element, InspectOptions? options = null, CancellationToken cancellationToken = default);
}