namespace WpfAutomation.App.Docking.Layout;

public sealed record DockLayoutNodeSnapshot
{
    public string NodeId { get; init; } = string.Empty;

    public string? GroupId { get; init; }

    public string? FirstChildNodeId { get; init; }

    public string? SecondChildNodeId { get; init; }

    public DockLayoutSplitOrientation SnapshotOrientation { get; init; } = DockLayoutSplitOrientation.Horizontal;

    public double Ratio { get; init; } = 0.5;
}
