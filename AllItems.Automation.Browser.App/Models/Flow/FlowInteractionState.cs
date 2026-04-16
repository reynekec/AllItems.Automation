namespace AllItems.Automation.Browser.App.Models.Flow;

public sealed record FlowInteractionState
{
    public string? HoveredEdgeId { get; init; }

    public string? HoveredLaneId { get; init; }

    public double HoverIndicatorX { get; init; }

    public double HoverIndicatorY { get; init; }

    public bool IsDropInsertPreviewVisible { get; init; }

    public IReadOnlyList<string> SelectedNodeIds { get; init; } = [];

    public IReadOnlyList<string> SelectedEdgeIds { get; init; } = [];
}
