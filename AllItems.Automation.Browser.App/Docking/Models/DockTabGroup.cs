namespace AllItems.Automation.Browser.App.Docking.Models;

/// <summary>
/// Represents a tab group that hosts one or more dockable panels.
/// </summary>
public sealed class DockTabGroup
{
    public string GroupId { get; init; } = string.Empty;

    public DockPlacement Placement { get; init; } = DockPlacement.Center;

    public string ActivePanelId { get; set; } = string.Empty;

    public IReadOnlyList<string> PanelIds { get; init; } = [];
}
