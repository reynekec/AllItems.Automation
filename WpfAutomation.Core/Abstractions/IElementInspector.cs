using WpfAutomation.Core.Inspection;
using WpfAutomation.Core.Reports;

namespace WpfAutomation.Core.Abstractions;

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