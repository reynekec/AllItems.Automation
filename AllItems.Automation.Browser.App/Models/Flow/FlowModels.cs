namespace AllItems.Automation.Browser.App.Models.Flow;

public static class FlowDocumentSchema
{
    public const int CurrentVersion = 1;
}

public static class FlowLaneIdentifiers
{
    public const string RootLaneId = "root";
}

public sealed record FlowDocumentModel
{
    public string DocumentId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int SchemaVersion { get; init; } = FlowDocumentSchema.CurrentVersion;

    public FlowViewportModel Viewport { get; init; } = new();

    public FlowLaneModel RootLane { get; init; } = FlowLaneModel.CreateRoot();

    public IReadOnlyList<FlowNodeModel> Nodes { get; init; } = [];

    public IReadOnlyList<FlowEdgeModel> Edges { get; init; } = [];

    public FlowSelectionModel Selection { get; init; } = new();
}

public abstract record FlowNodeModel
{
    public abstract FlowNodeKind NodeKind { get; }

    public string NodeId { get; init; } = string.Empty;

    public string DisplayLabel { get; init; } = string.Empty;

    public bool IsCollapsed { get; init; }

    public FlowNodeBounds Bounds { get; init; } = new();

    public string? ParentContainerNodeId { get; init; }
}

public sealed record FlowActionNodeModel : FlowNodeModel
{
    public override FlowNodeKind NodeKind => FlowNodeKind.Action;

    public FlowActionReferenceModel ActionReference { get; init; } = new();

    public ActionParameters ActionParameters { get; init; } = new UnknownActionParameters();
}

public sealed record FlowContainerNodeModel : FlowNodeModel
{
    public override FlowNodeKind NodeKind => FlowNodeKind.Container;

    public FlowContainerKind ContainerKind { get; init; } = FlowContainerKind.Group;

    public ContainerParameters ContainerParameters { get; init; } = new UnknownContainerParameters();

    public bool IsExpanded { get; init; } = true;

    public IReadOnlyList<FlowLaneModel> ChildLanes { get; init; } = [];
}

public sealed record FlowActionReferenceModel
{
    public string ActionId { get; init; } = string.Empty;

    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public string IconKeyOrPath { get; init; } = string.Empty;

    public IReadOnlyList<string> Keywords { get; init; } = [];
}

public sealed record FlowLaneModel
{
    public string LaneId { get; init; } = string.Empty;

    public FlowLaneKind LaneKind { get; init; } = FlowLaneKind.Sequential;

    public string DisplayLabel { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public string? ParentContainerNodeId { get; init; }

    public double VisualHeight { get; init; } = 37;

    public IReadOnlyList<string> NodeIds { get; init; } = [];

    public static FlowLaneModel CreateRoot()
    {
        return new FlowLaneModel
        {
            LaneId = FlowLaneIdentifiers.RootLaneId,
            LaneKind = FlowLaneKind.Root,
            DisplayLabel = "Root",
            SortOrder = 0,
            VisualHeight = 0,
            NodeIds = [],
        };
    }
}

public sealed record FlowEdgeModel
{
    public string EdgeId { get; init; } = string.Empty;

    public string FromNodeId { get; init; } = string.Empty;

    public string ToNodeId { get; init; } = string.Empty;

    public FlowPortKind FromPort { get; init; } = FlowPortKind.Output;

    public FlowPortKind ToPort { get; init; } = FlowPortKind.Input;

    public FlowEdgeLaneMetadataModel? LaneMetadata { get; init; }
}

public sealed record FlowEdgeLaneMetadataModel
{
    public string? SourceLaneId { get; init; }

    public string? TargetLaneId { get; init; }

    public string? OwningContainerNodeId { get; init; }
}

public sealed record FlowViewportModel
{
    public double OffsetX { get; init; }

    public double OffsetY { get; init; }

    public double Zoom { get; init; } = 1.0;
}

public sealed record FlowSelectionModel
{
    public string? PrimaryNodeId { get; init; }

    public string? PrimaryEdgeId { get; init; }

    public IReadOnlyList<string> SelectedNodeIds { get; init; } = [];

    public IReadOnlyList<string> SelectedEdgeIds { get; init; } = [];
}

public sealed record FlowNodeBounds
{
    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }
}

public enum FlowNodeKind
{
    Action = 0,
    Container = 1,
}

public enum FlowContainerKind
{
    Group = 0,
    Loop = 1,
    Condition = 2,
    For = 3,
    ForEach = 4,
    While = 5,
}

public enum FlowLaneKind
{
    Root = 0,
    Sequential = 1,
    LoopBody = 2,
    ConditionTrue = 3,
    ConditionFalse = 4,
}

public enum FlowPortKind
{
    Input = 0,
    Output = 1,
    TrueBranch = 2,
    FalseBranch = 3,
    LoopBody = 4,
}