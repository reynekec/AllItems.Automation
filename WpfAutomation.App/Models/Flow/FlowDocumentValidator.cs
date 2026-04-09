namespace WpfAutomation.App.Models.Flow;

public static class FlowDocumentValidator
{
    public static FlowDocumentValidationResult Validate(FlowDocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<string> errors = [];
        Dictionary<string, FlowNodeModel> nodesById = new(StringComparer.Ordinal);
        HashSet<string> edgeIds = new(StringComparer.Ordinal);
        HashSet<string> laneIds = new(StringComparer.Ordinal);
        Dictionary<string, string?> laneOwnershipByNodeId = new(StringComparer.Ordinal);
        Dictionary<string, string?> laneOwnershipByLaneId = new(StringComparer.Ordinal);

        if (document.SchemaVersion <= 0)
        {
            errors.Add("Schema version must be greater than zero.");
        }

        foreach (var node in document.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                errors.Add("Every node must have a stable NodeId.");
                continue;
            }

            if (!nodesById.TryAdd(node.NodeId, node))
            {
                errors.Add($"Duplicate node id '{node.NodeId}'.");
            }

            if (node.Bounds.Width < 0 || node.Bounds.Height < 0)
            {
                errors.Add($"Node '{node.NodeId}' has invalid bounds.");
            }
        }

        ValidateLane(document.RootLane, parentContainerNodeId: null, laneIds, laneOwnershipByNodeId, laneOwnershipByLaneId, errors);

        foreach (var container in document.Nodes.OfType<FlowContainerNodeModel>())
        {
            if (container.IsCollapsed == container.IsExpanded)
            {
                errors.Add($"Container node '{container.NodeId}' must keep IsCollapsed and IsExpanded in opposite states.");
            }

            var siblingSortOrders = new HashSet<int>();
            foreach (var lane in container.ChildLanes)
            {
                if (!siblingSortOrders.Add(lane.SortOrder))
                {
                    errors.Add($"Container node '{container.NodeId}' contains duplicate lane sort order '{lane.SortOrder}'.");
                }

                ValidateLane(lane, container.NodeId, laneIds, laneOwnershipByNodeId, laneOwnershipByLaneId, errors);
            }

            ValidateContainerSemantics(container, errors);
        }

        foreach (var node in document.Nodes)
        {
            if (!laneOwnershipByNodeId.TryGetValue(node.NodeId, out var owningContainerNodeId))
            {
                errors.Add($"Node '{node.NodeId}' is not owned by the root lane or a container lane.");
                continue;
            }

            if (!string.Equals(node.ParentContainerNodeId, owningContainerNodeId, StringComparison.Ordinal))
            {
                errors.Add($"Node '{node.NodeId}' has parent ownership that does not match its lane assignment.");
            }
        }

        foreach (var edge in document.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.EdgeId))
            {
                errors.Add("Every edge must have a stable EdgeId.");
            }
            else if (!edgeIds.Add(edge.EdgeId))
            {
                errors.Add($"Duplicate edge id '{edge.EdgeId}'.");
            }

            if (!nodesById.ContainsKey(edge.FromNodeId))
            {
                errors.Add($"Edge '{edge.EdgeId}' references missing source node '{edge.FromNodeId}'.");
            }

            if (!nodesById.ContainsKey(edge.ToNodeId))
            {
                errors.Add($"Edge '{edge.EdgeId}' references missing target node '{edge.ToNodeId}'.");
            }

            if (edge.LaneMetadata is not null)
            {
                if (edge.LaneMetadata.SourceLaneId is not null && !laneIds.Contains(edge.LaneMetadata.SourceLaneId))
                {
                    errors.Add($"Edge '{edge.EdgeId}' references missing source lane '{edge.LaneMetadata.SourceLaneId}'.");
                }

                if (edge.LaneMetadata.TargetLaneId is not null && !laneIds.Contains(edge.LaneMetadata.TargetLaneId))
                {
                    errors.Add($"Edge '{edge.EdgeId}' references missing target lane '{edge.LaneMetadata.TargetLaneId}'.");
                }

                if (edge.LaneMetadata.OwningContainerNodeId is not null && !nodesById.ContainsKey(edge.LaneMetadata.OwningContainerNodeId))
                {
                    errors.Add($"Edge '{edge.EdgeId}' references missing owning container '{edge.LaneMetadata.OwningContainerNodeId}'.");
                }

                if (edge.LaneMetadata.SourceLaneId is not null &&
                    laneOwnershipByLaneId.TryGetValue(edge.LaneMetadata.SourceLaneId, out var sourceLaneOwner) &&
                    !string.Equals(sourceLaneOwner, edge.LaneMetadata.OwningContainerNodeId, StringComparison.Ordinal))
                {
                    errors.Add($"Edge '{edge.EdgeId}' source lane ownership does not match owning container metadata.");
                }

                if (edge.LaneMetadata.TargetLaneId is not null &&
                    laneOwnershipByLaneId.TryGetValue(edge.LaneMetadata.TargetLaneId, out var targetLaneOwner) &&
                    !string.Equals(targetLaneOwner, edge.LaneMetadata.OwningContainerNodeId, StringComparison.Ordinal))
                {
                    errors.Add($"Edge '{edge.EdgeId}' target lane ownership does not match owning container metadata.");
                }
            }
        }

        if (document.Selection.PrimaryNodeId is not null && !nodesById.ContainsKey(document.Selection.PrimaryNodeId))
        {
            errors.Add($"Selection references missing primary node '{document.Selection.PrimaryNodeId}'.");
        }

        if (document.Selection.PrimaryEdgeId is not null && !edgeIds.Contains(document.Selection.PrimaryEdgeId))
        {
            errors.Add($"Selection references missing primary edge '{document.Selection.PrimaryEdgeId}'.");
        }

        foreach (var selectedNodeId in document.Selection.SelectedNodeIds)
        {
            if (!nodesById.ContainsKey(selectedNodeId))
            {
                errors.Add($"Selection references missing node '{selectedNodeId}'.");
            }
        }

        foreach (var selectedEdgeId in document.Selection.SelectedEdgeIds)
        {
            if (!edgeIds.Contains(selectedEdgeId))
            {
                errors.Add($"Selection references missing edge '{selectedEdgeId}'.");
            }
        }

        return new FlowDocumentValidationResult(errors);
    }

    private static void ValidateLane(
        FlowLaneModel lane,
        string? parentContainerNodeId,
        HashSet<string> laneIds,
        Dictionary<string, string?> laneOwnershipByNodeId,
        Dictionary<string, string?> laneOwnershipByLaneId,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(lane.LaneId))
        {
            errors.Add("Every lane must have a stable LaneId.");
        }
        else if (!laneIds.Add(lane.LaneId))
        {
            errors.Add($"Duplicate lane id '{lane.LaneId}'.");
        }
        else
        {
            laneOwnershipByLaneId[lane.LaneId] = parentContainerNodeId;
        }

        if (!string.Equals(lane.ParentContainerNodeId, parentContainerNodeId, StringComparison.Ordinal))
        {
            errors.Add($"Lane '{lane.LaneId}' has parent ownership that does not match its containing node.");
        }

        var orderedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nodeId in lane.NodeIds)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                errors.Add($"Lane '{lane.LaneId}' contains an empty node id entry.");
                continue;
            }

            if (!orderedNodeIds.Add(nodeId))
            {
                errors.Add($"Lane '{lane.LaneId}' contains duplicate node id '{nodeId}'.");
            }

            if (!laneOwnershipByNodeId.TryAdd(nodeId, parentContainerNodeId))
            {
                errors.Add($"Node '{nodeId}' is assigned to multiple lanes.");
            }
        }
    }

    private static void ValidateContainerSemantics(FlowContainerNodeModel container, ICollection<string> errors)
    {
        if (container.ContainerKind == FlowContainerKind.Condition)
        {
            var hasTrue = container.ChildLanes.Any(lane => lane.LaneKind == FlowLaneKind.ConditionTrue);
            var hasFalse = container.ChildLanes.Any(lane => lane.LaneKind == FlowLaneKind.ConditionFalse);
            if (!hasTrue || !hasFalse)
            {
                errors.Add($"Condition container '{container.NodeId}' must define both true and false lanes.");
            }
        }

        var requiresLoopBodyLane = container.ContainerKind is FlowContainerKind.Loop
            or FlowContainerKind.For
            or FlowContainerKind.ForEach
            or FlowContainerKind.While;

        if (requiresLoopBodyLane)
        {
            var hasLoopBody = container.ChildLanes.Any(lane => lane.LaneKind == FlowLaneKind.LoopBody);
            if (!hasLoopBody)
            {
                errors.Add($"Container '{container.NodeId}' of kind '{container.ContainerKind}' must define a loop body lane.");
            }
        }

        switch (container.ContainerKind)
        {
            case FlowContainerKind.For:
                if (container.ContainerParameters is not ForContainerParameters parameters)
                {
                    errors.Add($"For container '{container.NodeId}' must use {nameof(ForContainerParameters)}.");
                    return;
                }

                if (parameters.Step == 0)
                {
                    errors.Add($"For container '{container.NodeId}' must use a non-zero step.");
                }

                if (parameters.MaxIterationsOverride.HasValue && parameters.MaxIterationsOverride.Value <= 0)
                {
                    errors.Add($"For container '{container.NodeId}' max iteration override must be greater than zero.");
                }

                break;
            case FlowContainerKind.ForEach:
                if (container.ContainerParameters is not ForEachContainerParameters forEachParameters)
                {
                    errors.Add($"ForEach container '{container.NodeId}' must use {nameof(ForEachContainerParameters)}.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(forEachParameters.ItemVariable))
                {
                    errors.Add($"ForEach container '{container.NodeId}' must define an item variable.");
                }

                if (forEachParameters.MaxIterationsOverride.HasValue && forEachParameters.MaxIterationsOverride.Value <= 0)
                {
                    errors.Add($"ForEach container '{container.NodeId}' max iteration override must be greater than zero.");
                }

                break;
            case FlowContainerKind.While:
                if (container.ContainerParameters is not WhileContainerParameters whileParameters)
                {
                    errors.Add($"While container '{container.NodeId}' must use {nameof(WhileContainerParameters)}.");
                    return;
                }

                if (whileParameters.MaxIterations <= 0)
                {
                    errors.Add($"While container '{container.NodeId}' max iterations must be greater than zero.");
                }

                if (string.IsNullOrWhiteSpace(whileParameters.ConditionExpression))
                {
                    errors.Add($"While container '{container.NodeId}' must define a condition expression.");
                }

                break;
        }
    }
}

public sealed record FlowDocumentValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}