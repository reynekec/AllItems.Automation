using System.Windows;

namespace WpfAutomation.App.Models.Flow;

public sealed record FlowDropContextModel
{
    public Point DropPoint { get; init; }

    public string? TargetLaneId { get; init; }

    public string? TargetEdgeId { get; init; }

    public string? TargetContainerNodeId { get; init; }

    public static FlowDropContextModel CreateRoot(Point dropPoint, string? targetEdgeId = null)
    {
        return new FlowDropContextModel
        {
            DropPoint = dropPoint,
            TargetLaneId = FlowLaneIdentifiers.RootLaneId,
            TargetEdgeId = targetEdgeId,
        };
    }
}