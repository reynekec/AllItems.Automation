namespace AllItems.Automation.Browser.App.Docking.Models;

/// <summary>
/// Describes a panel that can participate in the dock layout.
/// </summary>
public sealed class DockPanelDescriptor
{
    public string PanelId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string ContentKey { get; init; } = string.Empty;

    public bool IsClosable { get; init; } = true;

    public bool IsPinnable { get; init; } = true;

    public DockPanelKind PanelKind { get; init; } = DockPanelKind.ToolWindow;

    public bool ShowTabHeader { get; init; } = true;

    public bool IsInitiallyVisible { get; init; } = true;
}
