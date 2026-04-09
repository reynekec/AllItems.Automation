using WpfAutomation.App.Models.Flow;

namespace WpfAutomation.App.Services.Flow;

public interface IFlowDocumentMapper<out TExecutionGraph>
    where TExecutionGraph : IExecutionFlowGraph
{
    TExecutionGraph Map(FlowDocumentModel document, CancellationToken cancellationToken = default);
}

public interface IExecutionFlowGraph
{
    int SchemaVersion { get; }

    IReadOnlyList<IExecutionFlowNode> Nodes { get; }

    IReadOnlyList<IExecutionFlowEdge> Edges { get; }
}

public interface IExecutionFlowNode
{
    string ExecutionNodeId { get; }

    string SourceNodeId { get; }

    string DisplayLabel { get; }

    FlowNodeKind NodeKind { get; }

    string? ActionId { get; }

    FlowContainerKind? ContainerKind { get; }

    ContainerParameters? ContainerParameters { get; }

    IReadOnlyList<IExecutionFlowLane> ChildLanes { get; }
}

public interface IExecutionFlowLane
{
    FlowLaneKind LaneKind { get; }

    int SortOrder { get; }

    IReadOnlyList<string> NodeExecutionIds { get; }
}

public interface IExecutionFlowEdge
{
    string FromExecutionNodeId { get; }

    string ToExecutionNodeId { get; }

    FlowPortKind FromPort { get; }

    FlowPortKind ToPort { get; }
}