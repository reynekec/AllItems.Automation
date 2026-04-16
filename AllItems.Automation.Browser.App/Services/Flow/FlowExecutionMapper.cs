using AllItems.Automation.Browser.App.Models.Flow;

namespace AllItems.Automation.Browser.App.Services.Flow;

public sealed class FlowExecutionMapper : IFlowDocumentMapper<ExecutionFlowGraph>
{
    public ExecutionFlowGraph Map(FlowDocumentModel document, CancellationToken cancellationToken = default)
    {
        var validation = FlowDocumentValidator.Validate(document);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        var executionIdsBySourceNodeId = document.Nodes.ToDictionary(
            node => node.NodeId,
            node => $"exec-{node.NodeId}",
            StringComparer.Ordinal);

        var nodes = document.Nodes.Select(node =>
        {
            if (node is FlowActionNodeModel actionNode)
            {
                return (IExecutionFlowNode)new ExecutionFlowNode
                {
                    ExecutionNodeId = executionIdsBySourceNodeId[actionNode.NodeId],
                    SourceNodeId = actionNode.NodeId,
                    DisplayLabel = actionNode.DisplayLabel,
                    NodeKind = actionNode.NodeKind,
                    ActionId = actionNode.ActionReference.ActionId,
                    ActionParameters = actionNode.ActionParameters,
                    ContainerKind = null,
                    ContainerParameters = null,
                    ChildLanes = [],
                };
            }

            var containerNode = (FlowContainerNodeModel)node;
            return (IExecutionFlowNode)new ExecutionFlowNode
            {
                ExecutionNodeId = executionIdsBySourceNodeId[containerNode.NodeId],
                SourceNodeId = containerNode.NodeId,
                DisplayLabel = containerNode.DisplayLabel,
                NodeKind = containerNode.NodeKind,
                ActionId = null,
                ActionParameters = null,
                ContainerKind = containerNode.ContainerKind,
                ContainerParameters = containerNode.ContainerParameters,
                ChildLanes = containerNode.ChildLanes
                    .OrderBy(lane => lane.SortOrder)
                    .Select(lane => (IExecutionFlowLane)new ExecutionFlowLane
                    {
                        LaneKind = lane.LaneKind,
                        SortOrder = lane.SortOrder,
                        NodeExecutionIds = lane.NodeIds
                            .Where(nodeId => executionIdsBySourceNodeId.ContainsKey(nodeId))
                            .Select(nodeId => executionIdsBySourceNodeId[nodeId])
                            .ToList(),
                    })
                    .ToList(),
            };
        }).ToList();

        var edges = document.Edges.Select(edge => (IExecutionFlowEdge)new ExecutionFlowEdge
        {
            FromExecutionNodeId = executionIdsBySourceNodeId[edge.FromNodeId],
            ToExecutionNodeId = executionIdsBySourceNodeId[edge.ToNodeId],
            FromPort = edge.FromPort,
            ToPort = edge.ToPort,
        }).ToList();

        return new ExecutionFlowGraph
        {
            SchemaVersion = document.SchemaVersion,
            Nodes = nodes,
            Edges = edges,
        };
    }
}

public sealed record ExecutionFlowGraph : IExecutionFlowGraph
{
    public int SchemaVersion { get; init; }

    public IReadOnlyList<IExecutionFlowNode> Nodes { get; init; } = [];

    public IReadOnlyList<IExecutionFlowEdge> Edges { get; init; } = [];
}

public sealed record ExecutionFlowNode : IExecutionFlowNode
{
    public string ExecutionNodeId { get; init; } = string.Empty;

    public string SourceNodeId { get; init; } = string.Empty;

    public string DisplayLabel { get; init; } = string.Empty;

    public FlowNodeKind NodeKind { get; init; }

    public string? ActionId { get; init; }

    public ActionParameters? ActionParameters { get; init; }

    public FlowContainerKind? ContainerKind { get; init; }

    public ContainerParameters? ContainerParameters { get; init; }

    public IReadOnlyList<IExecutionFlowLane> ChildLanes { get; init; } = [];
}

public sealed record ExecutionFlowLane : IExecutionFlowLane
{
    public FlowLaneKind LaneKind { get; init; }

    public int SortOrder { get; init; }

    public IReadOnlyList<string> NodeExecutionIds { get; init; } = [];
}

public sealed record ExecutionFlowEdge : IExecutionFlowEdge
{
    public string FromExecutionNodeId { get; init; } = string.Empty;

    public string ToExecutionNodeId { get; init; } = string.Empty;

    public FlowPortKind FromPort { get; init; }

    public FlowPortKind ToPort { get; init; }
}
