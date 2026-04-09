using System.Windows;

namespace WpfAutomation.App.Models.Flow;

public sealed record FlowEdgeVisualModel
{
    public string EdgeId { get; init; } = string.Empty;

    public string FromNodeId { get; init; } = string.Empty;

    public string ToNodeId { get; init; } = string.Empty;

    public string PathData { get; init; } = string.Empty;

    public IReadOnlyList<Point> RoutePoints { get; init; } = [];

    public double MidpointX { get; init; }

    public double MidpointY { get; init; }
}
