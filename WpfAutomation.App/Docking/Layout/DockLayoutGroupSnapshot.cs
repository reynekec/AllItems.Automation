namespace WpfAutomation.App.Docking.Layout;

public sealed record DockLayoutGroupSnapshot
{
    public string GroupId { get; init; } = string.Empty;

    public string ActivePanelId { get; init; } = string.Empty;

    public IReadOnlyList<DockLayoutPanelSnapshot> Panels { get; init; } = [];
}
