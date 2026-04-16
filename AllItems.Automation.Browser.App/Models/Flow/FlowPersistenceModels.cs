namespace AllItems.Automation.Browser.App.Models.Flow;

public sealed record FlowDocumentSnapshot
{
    public int SchemaVersion { get; init; } = FlowDocumentSchema.CurrentVersion;

    public string DocumentId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public FlowViewportSnapshot Viewport { get; init; } = new();

    public FlowLaneSnapshot RootLane { get; init; } = FlowLaneSnapshot.CreateRoot();

    public IReadOnlyList<FlowNodeSnapshot> Nodes { get; init; } = [];

    public IReadOnlyList<FlowEdgeSnapshot> Edges { get; init; } = [];

    public FlowSelectionSnapshot Selection { get; init; } = new();
}

public sealed record FlowNodeSnapshot
{
    public FlowNodeKind NodeKind { get; init; } = FlowNodeKind.Action;

    public string NodeId { get; init; } = string.Empty;

    public string DisplayLabel { get; init; } = string.Empty;

    public bool IsCollapsed { get; init; }

    public FlowNodeBoundsSnapshot Bounds { get; init; } = new();

    public string? ParentContainerNodeId { get; init; }

    public FlowActionReferenceSnapshot? ActionReference { get; init; }

    public FlowActionParametersSnapshot? ActionParameters { get; init; }

    public FlowContainerKind? ContainerKind { get; init; }

    public FlowContainerParametersSnapshot? ContainerParameters { get; init; }

    public bool? IsExpanded { get; init; }

    public IReadOnlyList<FlowLaneSnapshot> ChildLanes { get; init; } = [];
}

public sealed record FlowActionReferenceSnapshot
{
    public string ActionId { get; init; } = string.Empty;

    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public string IconKeyOrPath { get; init; } = string.Empty;

    public IReadOnlyList<string> Keywords { get; init; } = [];
}

public sealed record FlowLaneSnapshot
{
    public string LaneId { get; init; } = string.Empty;

    public FlowLaneKind LaneKind { get; init; } = FlowLaneKind.Sequential;

    public string DisplayLabel { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public string? ParentContainerNodeId { get; init; }

    public IReadOnlyList<string> NodeIds { get; init; } = [];

    public static FlowLaneSnapshot CreateRoot()
    {
        return new FlowLaneSnapshot
        {
            LaneId = FlowLaneIdentifiers.RootLaneId,
            LaneKind = FlowLaneKind.Root,
            DisplayLabel = "Root",
            SortOrder = 0,
            NodeIds = [],
        };
    }
}

public sealed record FlowEdgeSnapshot
{
    public string EdgeId { get; init; } = string.Empty;

    public string FromNodeId { get; init; } = string.Empty;

    public string ToNodeId { get; init; } = string.Empty;

    public FlowPortKind FromPort { get; init; } = FlowPortKind.Output;

    public FlowPortKind ToPort { get; init; } = FlowPortKind.Input;

    public FlowEdgeLaneMetadataSnapshot? LaneMetadata { get; init; }
}

public sealed record FlowEdgeLaneMetadataSnapshot
{
    public string? SourceLaneId { get; init; }

    public string? TargetLaneId { get; init; }

    public string? OwningContainerNodeId { get; init; }
}

public sealed record FlowViewportSnapshot
{
    public double OffsetX { get; init; }

    public double OffsetY { get; init; }

    public double Zoom { get; init; } = 1.0;
}

public sealed record FlowSelectionSnapshot
{
    public string? PrimaryNodeId { get; init; }

    public string? PrimaryEdgeId { get; init; }

    public IReadOnlyList<string> SelectedNodeIds { get; init; } = [];

    public IReadOnlyList<string> SelectedEdgeIds { get; init; } = [];
}

public sealed record FlowNodeBoundsSnapshot
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }
}

public sealed record FlowActionParametersSnapshot
{
    public string ParameterTypeName { get; init; } = string.Empty;

    public string JsonPayload { get; init; } = "{}";
}

public sealed record FlowContainerParametersSnapshot
{
    public string ParameterTypeName { get; init; } = string.Empty;

    public string JsonPayload { get; init; } = "{}";
}