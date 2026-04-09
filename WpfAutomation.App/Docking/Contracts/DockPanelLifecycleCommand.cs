using WpfAutomation.App.Docking.Models;

namespace WpfAutomation.App.Docking.Contracts;

/// <summary>
/// Represents an intent to transition a panel through one lifecycle action.
/// </summary>
public sealed record DockPanelLifecycleCommand
{
    public string PanelId { get; init; } = string.Empty;

    public DockPanelLifecycleAction Action { get; init; }

    public string? GroupId { get; init; }

    public DockPlacement? Placement { get; init; }
}
