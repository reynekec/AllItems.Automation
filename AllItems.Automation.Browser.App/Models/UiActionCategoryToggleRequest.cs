namespace AllItems.Automation.Browser.App.Models;

/// <summary>
/// Payload describing a category expand or collapse transition.
/// </summary>
public sealed class UiActionCategoryToggleRequest
{
    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public bool IsExpanded { get; init; }
}