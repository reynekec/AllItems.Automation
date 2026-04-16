namespace AllItems.Automation.Browser.App.Models;

/// <summary>
/// Payload sent to the parent when an action is selected from the sidebar.
/// </summary>
public sealed class UiActionInvokeRequest
{
    public string ActionId { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;
}