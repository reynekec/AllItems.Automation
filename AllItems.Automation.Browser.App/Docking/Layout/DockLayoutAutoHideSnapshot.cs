namespace AllItems.Automation.Browser.App.Docking.Layout;

public sealed record DockLayoutAutoHideSnapshot
{
    public string PanelId { get; init; } = string.Empty;

    public DockLayoutAutoHidePlacement Placement { get; init; } = DockLayoutAutoHidePlacement.Left;

    public int Order { get; init; }
}
