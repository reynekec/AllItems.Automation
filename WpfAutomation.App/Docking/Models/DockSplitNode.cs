namespace WpfAutomation.App.Docking.Models;

/// <summary>
/// Represents a split node in a dock layout tree.
/// </summary>
public sealed class DockSplitNode
{
    public string NodeId { get; init; } = string.Empty;

    public DockSplitOrientation Orientation { get; init; } = DockSplitOrientation.Horizontal;

    public double Ratio { get; init; } = 0.5;

    public string? FirstChildNodeId { get; init; }

    public string? SecondChildNodeId { get; init; }

    public string? GroupId { get; init; }
}
