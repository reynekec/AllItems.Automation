namespace WpfAutomation.App.Docking.Models;

/// <summary>
/// Represents one auto-hidden panel entry on a strip edge.
/// </summary>
public sealed class DockAutoHideStripItem
{
    public string PanelId { get; init; } = string.Empty;

    public DockPlacement StripPlacement { get; init; } = DockPlacement.Left;

    public int Order { get; init; }
}
