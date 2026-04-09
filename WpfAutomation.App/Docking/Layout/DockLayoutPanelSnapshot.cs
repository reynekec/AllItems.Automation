namespace WpfAutomation.App.Docking.Layout;

public sealed record DockLayoutPanelSnapshot
{
    public string PanelId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public bool IsPinned { get; init; } = true;

    public int TabOrder { get; init; }
}
