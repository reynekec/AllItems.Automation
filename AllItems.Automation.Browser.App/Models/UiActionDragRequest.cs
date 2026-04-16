namespace AllItems.Automation.Browser.App.Models;

/// <summary>
/// Payload emitted when the user initiates drag from an action row.
/// </summary>
public sealed class UiActionDragRequest
{
    public string ActionId { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public bool IsContainer { get; init; }
}