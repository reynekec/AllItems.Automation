using AllItems.Automation.Browser.App.Docking.Models;

namespace AllItems.Automation.Browser.App.Docking.Contracts;

/// <summary>
/// Raised after a lifecycle action has been applied to a panel.
/// </summary>
public sealed record DockPanelLifecycleEvent
{
    public string PanelId { get; init; } = string.Empty;

    public DockPanelLifecycleAction Action { get; init; }

    public string? GroupId { get; init; }

    public DockPlacement? Placement { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
