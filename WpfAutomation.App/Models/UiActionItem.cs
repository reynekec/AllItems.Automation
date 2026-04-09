namespace WpfAutomation.App.Models;

/// <summary>
/// Represents a single actionable item surfaced in the sidebar catalog.
/// </summary>
public sealed class UiActionItem
{
    public string ActionId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string IconKeyOrPath { get; init; } = string.Empty;

    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public bool IsContainer { get; init; }
}