using WpfAutomation.Core.Inspection;
using WpfAutomation.Core.Reports;

namespace WpfAutomation.Core.Abstractions;

/// <summary>
/// Inspects the current page and frame hierarchy.
/// </summary>
public interface IPageInspector
{
    /// <summary>
    /// Runs page-level inspection using the provided options.
    /// </summary>
    Task<PageInspectionReport> InspectAsync(PageInspectOptions? options = null, CancellationToken cancellationToken = default);
}