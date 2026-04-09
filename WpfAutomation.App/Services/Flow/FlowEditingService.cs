using WpfAutomation.App.Models;
using WpfAutomation.App.Models.Flow;

namespace WpfAutomation.App.Services.Flow;

public interface IFlowEditingService
{
    FlowDocumentModel CreateEmptyDocument(string? displayName = null);

    FlowDocumentModel AddActionNode(FlowDocumentModel document, UiActionDragRequest request, double x, double y, string? insertOnEdgeId = null, FlowDropContextModel? dropContext = null);

    FlowDocumentModel AddContainerNode(FlowDocumentModel document, FlowContainerKind kind, double x, double y);

    FlowDocumentModel DeleteSelection(FlowDocumentModel document, IReadOnlyList<string> nodeIds, IReadOnlyList<string> edgeIds);

    FlowClipboardModel CopySelection(FlowDocumentModel document, IReadOnlyList<string> nodeIds, IReadOnlyList<string> edgeIds);

    FlowDocumentModel PasteSelection(FlowDocumentModel document, FlowClipboardModel clipboard, double dx, double dy);

    FlowDocumentModel MoveNodesToLane(FlowDocumentModel document, IReadOnlyList<string> nodeIds, string laneId, int insertIndex);

    FlowDocumentModel TranslateNodes(FlowDocumentModel document, IReadOnlyList<string> nodeIds, double deltaX, double deltaY);
}

public sealed class FlowEditingService : IFlowEditingService
{
    private readonly IFlowActionParameterResolver _actionParameterResolver;

    public FlowEditingService()
        : this(new FlowActionParameterResolver())
    {
    }

    public FlowEditingService(IFlowActionParameterResolver actionParameterResolver)
    {
        _actionParameterResolver = actionParameterResolver;
    }

    public FlowDocumentModel CreateEmptyDocument(string? displayName = null)
    {
        return new FlowDocumentModel
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "New Flow" : displayName,
            RootLane = FlowLaneModel.CreateRoot(),
            Nodes = [],
            Edges = [],
            Selection = new FlowSelectionModel(),
        };
    }

    public FlowDocumentModel AddActionNode(FlowDocumentModel document, UiActionDragRequest request, double x, double y, string? insertOnEdgeId = null, FlowDropContextModel? dropContext = null)
    {
        var nodes = document.Nodes.ToList();
        var edges = document.Edges.ToList();
        var nodeId = request.IsContainer ? $"container-{Guid.NewGuid():N}" : $"node-{Guid.NewGuid():N}";

        var resolvedContext = dropContext ?? FlowDropContextModel.CreateRoot(new System.Windows.Point(x, y), insertOnEdgeId);
        var targetLaneId = string.IsNullOrWhiteSpace(resolvedContext.TargetLaneId)
            ? FlowLaneIdentifiers.RootLaneId
            : resolvedContext.TargetLaneId;

        var targetLane = FindLane(document, targetLaneId!);
        var laneMetadata = BuildLaneMetadata(document, targetLane);
        var nodeParentContainerId = targetLane.ParentContainerNodeId;

        var createdNode = CreateNodeFromDropRequest(request, nodeId, x, y) with { ParentContainerNodeId = nodeParentContainerId };
        nodes.Add(createdNode);

        var laneNodeIds = targetLane.NodeIds.ToList();
        var effectiveEdgeId = string.IsNullOrWhiteSpace(resolvedContext.TargetEdgeId)
            ? insertOnEdgeId
            : resolvedContext.TargetEdgeId;

        if (!string.IsNullOrWhiteSpace(effectiveEdgeId))
        {
            var hitEdge = edges.FirstOrDefault(edge => string.Equals(edge.EdgeId, effectiveEdgeId, StringComparison.Ordinal));
            if (hitEdge is not null)
            {
                var sourceNode = nodes.FirstOrDefault(node => string.Equals(node.NodeId, hitEdge.FromNodeId, StringComparison.Ordinal));
                if (sourceNode is not null && IsRootLane(targetLaneId!))
                {
                    nodes = CenterNodeUnderReference(nodes, nodeId, sourceNode);
                }

                edges.Remove(hitEdge);
                var effectiveMetadata = hitEdge.LaneMetadata ?? laneMetadata;
                edges.Add(new FlowEdgeModel
                {
                    EdgeId = $"edge-{Guid.NewGuid():N}",
                    FromNodeId = hitEdge.FromNodeId,
                    ToNodeId = nodeId,
                    FromPort = hitEdge.FromPort,
                    ToPort = FlowPortKind.Input,
                    LaneMetadata = effectiveMetadata,
                });
                edges.Add(new FlowEdgeModel
                {
                    EdgeId = $"edge-{Guid.NewGuid():N}",
                    FromNodeId = nodeId,
                    ToNodeId = hitEdge.ToNodeId,
                    FromPort = FlowPortKind.Output,
                    ToPort = hitEdge.ToPort,
                    LaneMetadata = effectiveMetadata,
                });

                var fromIndex = laneNodeIds.IndexOf(hitEdge.FromNodeId);
                if (fromIndex >= 0)
                {
                    laneNodeIds.Insert(fromIndex + 1, nodeId);
                }
                else
                {
                    laneNodeIds.Add(nodeId);
                }

                nodes = ShiftDownstreamLaneNodesForEdgeInsert(nodes, laneNodeIds, nodeId, hitEdge.ToNodeId);
            }
            else
            {
                if (IsRootLane(targetLaneId!) && laneNodeIds.Count > 0)
                {
                    var previousNodeId = laneNodeIds[^1];
                    var previousNode = nodes.FirstOrDefault(node => string.Equals(node.NodeId, previousNodeId, StringComparison.Ordinal));
                    if (previousNode is not null)
                    {
                        nodes = CenterNodeUnderReference(nodes, nodeId, previousNode);
                    }
                }

                AppendToLane(laneNodeIds, edges, nodeId, laneMetadata);
            }
        }
        else
        {
            if (IsRootLane(targetLaneId!) && laneNodeIds.Count > 0)
            {
                var previousNodeId = laneNodeIds[^1];
                var previousNode = nodes.FirstOrDefault(node => string.Equals(node.NodeId, previousNodeId, StringComparison.Ordinal));
                if (previousNode is not null)
                {
                    nodes = CenterNodeUnderReference(nodes, nodeId, previousNode);
                }
            }

            AppendToLane(laneNodeIds, edges, nodeId, laneMetadata);
        }

        var (updatedRootLane, updatedNodes) = ApplyLaneNodeIds(document.RootLane, nodes, targetLaneId!, laneNodeIds);

        return document with
        {
            Nodes = LayoutContainers(updatedNodes),
            Edges = DeduplicateEdges(edges),
            RootLane = updatedRootLane,
            Selection = new FlowSelectionModel
            {
                PrimaryNodeId = nodeId,
                SelectedNodeIds = [nodeId],
                SelectedEdgeIds = [],
            },
        };
    }

    private static void AppendToLane(List<string> laneNodeIds, List<FlowEdgeModel> edges, string nodeId, FlowEdgeLaneMetadataModel? laneMetadata)
    {
        if (laneNodeIds.Count > 0)
        {
            var previousNodeId = laneNodeIds[^1];
            edges.Add(new FlowEdgeModel
            {
                EdgeId = $"edge-{Guid.NewGuid():N}",
                FromNodeId = previousNodeId,
                ToNodeId = nodeId,
                LaneMetadata = laneMetadata,
            });
        }

        laneNodeIds.Add(nodeId);
    }

    private static bool IsRootLane(string laneId)
    {
        return string.Equals(laneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal);
    }

    private static List<FlowNodeModel> CenterNodeUnderReference(
        List<FlowNodeModel> nodes,
        string nodeId,
        FlowNodeModel referenceNode)
    {
        var referenceCenterX = referenceNode.Bounds.X + (referenceNode.Bounds.Width / 2d);

        return nodes.Select(node =>
        {
            if (!string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
            {
                return node;
            }

            return node with
            {
                Bounds = node.Bounds with
                {
                    X = referenceCenterX - (node.Bounds.Width / 2d),
                },
            };
        }).ToList();
    }

    private static (FlowLaneModel RootLane, List<FlowNodeModel> Nodes) ApplyLaneNodeIds(
        FlowLaneModel rootLane,
        List<FlowNodeModel> nodes,
        string targetLaneId,
        List<string> laneNodeIds)
    {
        if (string.Equals(targetLaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            return (rootLane with { NodeIds = laneNodeIds }, nodes);
        }

        var rewrittenNodes = nodes.Select(node =>
        {
            if (node is not FlowContainerNodeModel container)
            {
                return node;
            }

            var updatedLanes = container.ChildLanes.Select(lane =>
            {
                if (!string.Equals(lane.LaneId, targetLaneId, StringComparison.Ordinal))
                {
                    return lane;
                }

                return lane with { NodeIds = laneNodeIds };
            }).ToList();

            return (FlowNodeModel)(container with { ChildLanes = updatedLanes });
        }).ToList();

        return (rootLane, rewrittenNodes);
    }

    private static FlowLaneModel FindLane(FlowDocumentModel document, string laneId)
    {
        if (string.Equals(laneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            return document.RootLane;
        }

        var lane = document.Nodes
            .OfType<FlowContainerNodeModel>()
            .SelectMany(container => container.ChildLanes)
            .FirstOrDefault(candidate => string.Equals(candidate.LaneId, laneId, StringComparison.Ordinal));

        return lane ?? document.RootLane;
    }

    private static FlowEdgeLaneMetadataModel? BuildLaneMetadata(FlowDocumentModel document, FlowLaneModel lane)
    {
        if (string.Equals(lane.LaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            return null;
        }

        var owningContainerNodeId = lane.ParentContainerNodeId;
        if (string.IsNullOrWhiteSpace(owningContainerNodeId))
        {
            owningContainerNodeId = document.Nodes
                .OfType<FlowContainerNodeModel>()
                .Where(container => container.ChildLanes.Any(child => string.Equals(child.LaneId, lane.LaneId, StringComparison.Ordinal)))
                .Select(container => container.NodeId)
                .FirstOrDefault();
        }

        return new FlowEdgeLaneMetadataModel
        {
            SourceLaneId = lane.LaneId,
            TargetLaneId = lane.LaneId,
            OwningContainerNodeId = owningContainerNodeId,
        };
    }

    private static List<FlowNodeModel> ShiftDownstreamLaneNodesForEdgeInsert(
        List<FlowNodeModel> nodes,
        IReadOnlyList<string> laneNodeIds,
        string insertedNodeId,
        string downstreamNodeId)
    {
        var insertedNode = nodes.FirstOrDefault(node => string.Equals(node.NodeId, insertedNodeId, StringComparison.Ordinal));
        if (insertedNode is null)
        {
            return nodes;
        }

        var downstreamIndex = laneNodeIds
            .Select((nodeId, index) => new { nodeId, index })
            .Where(pair => string.Equals(pair.nodeId, downstreamNodeId, StringComparison.Ordinal))
            .Select(pair => pair.index)
            .DefaultIfEmpty(-1)
            .First();
        if (downstreamIndex < 0)
        {
            return nodes;
        }

        var downstreamNode = nodes.FirstOrDefault(node => string.Equals(node.NodeId, downstreamNodeId, StringComparison.Ordinal));
        if (downstreamNode is null)
        {
            return nodes;
        }

        const double verticalGap = 28d;
        var requiredDownstreamTop = insertedNode.Bounds.Y + insertedNode.Bounds.Height + verticalGap;
        var deltaY = requiredDownstreamTop - downstreamNode.Bounds.Y;
        if (deltaY <= 0)
        {
            return nodes;
        }

        var nodesToShift = new HashSet<string>(laneNodeIds.Skip(downstreamIndex), StringComparer.Ordinal);

        return nodes.Select(node =>
        {
            if (!nodesToShift.Contains(node.NodeId) || string.Equals(node.NodeId, insertedNodeId, StringComparison.Ordinal))
            {
                return node;
            }

            return node with
            {
                Bounds = node.Bounds with { Y = node.Bounds.Y + deltaY },
            };
        }).ToList();
    }

    private FlowNodeModel CreateNodeFromDropRequest(UiActionDragRequest request, string nodeId, double x, double y)
    {
        if (!request.IsContainer)
        {
            var defaultParameters = _actionParameterResolver.Resolve(request.ActionId).DefaultValue;

            return new FlowActionNodeModel
            {
                NodeId = nodeId,
                DisplayLabel = request.ActionName,
                Bounds = new FlowNodeBounds { X = x, Y = y, Width = 380, Height = 50 },
                ActionReference = new FlowActionReferenceModel
                {
                    ActionId = request.ActionId,
                    CategoryId = request.CategoryId,
                    CategoryName = request.CategoryName,
                    Keywords = [request.ActionName],
                },
                ActionParameters = defaultParameters,
            };
        }

        var containerKind = ResolveContainerKind(request.ActionId);

        return new FlowContainerNodeModel
        {
            NodeId = nodeId,
            DisplayLabel = request.ActionName,
            ContainerKind = containerKind,
            ContainerParameters = CreateDefaultContainerParameters(containerKind),
            IsExpanded = true,
            IsCollapsed = false,
            Bounds = new FlowNodeBounds
            {
                X = x,
                Y = y,
                Width = 420,
                Height = containerKind == FlowContainerKind.Condition ? 190 : 150,
            },
            ChildLanes = CreateDefaultLanes(containerKind, nodeId),
        };
    }

    public FlowDocumentModel AddContainerNode(FlowDocumentModel document, FlowContainerKind kind, double x, double y)
    {
        var nodes = document.Nodes.ToList();
        var rootNodeIds = document.RootLane.NodeIds.ToList();
        var containerId = $"container-{Guid.NewGuid():N}";

        var lanes = CreateDefaultLanes(kind, containerId);

        var container = new FlowContainerNodeModel
        {
            NodeId = containerId,
            DisplayLabel = kind.ToString(),
            ContainerKind = kind,
            ContainerParameters = CreateDefaultContainerParameters(kind),
            IsExpanded = true,
            IsCollapsed = false,
            Bounds = new FlowNodeBounds
            {
                X = x,
                Y = y,
                Width = 420,
                Height = kind == FlowContainerKind.Condition ? 190 : 150,
            },
            ChildLanes = lanes,
        };

        nodes.Add(container);

        if (rootNodeIds.Count > 0)
        {
            var predecessorId = rootNodeIds[^1];
            var predecessor = nodes.FirstOrDefault(node => string.Equals(node.NodeId, predecessorId, StringComparison.Ordinal));
            if (predecessor is not null)
            {
                nodes = CenterNodeUnderReference(nodes, containerId, predecessor);
            }

            var edges = document.Edges.ToList();
            edges.Add(new FlowEdgeModel
            {
                EdgeId = $"edge-{Guid.NewGuid():N}",
                FromNodeId = predecessorId,
                ToNodeId = containerId,
            });

            rootNodeIds.Add(containerId);
            return document with
            {
                Nodes = nodes,
                Edges = DeduplicateEdges(edges),
                RootLane = document.RootLane with { NodeIds = rootNodeIds },
                Selection = new FlowSelectionModel { PrimaryNodeId = containerId, SelectedNodeIds = [containerId] },
            };
        }

        rootNodeIds.Add(containerId);
        return document with
        {
            Nodes = nodes,
            RootLane = document.RootLane with { NodeIds = rootNodeIds },
            Selection = new FlowSelectionModel { PrimaryNodeId = containerId, SelectedNodeIds = [containerId] },
        };
    }

    private static IReadOnlyList<FlowLaneModel> CreateDefaultLanes(FlowContainerKind kind, string containerId)
    {
        return kind switch
        {
            FlowContainerKind.Condition =>
            [
                new FlowLaneModel
                {
                    LaneId = $"lane-{Guid.NewGuid():N}",
                    ParentContainerNodeId = containerId,
                    LaneKind = FlowLaneKind.ConditionTrue,
                    DisplayLabel = "If yes",
                    SortOrder = 0,
                    NodeIds = [],
                },
                new FlowLaneModel
                {
                    LaneId = $"lane-{Guid.NewGuid():N}",
                    ParentContainerNodeId = containerId,
                    LaneKind = FlowLaneKind.ConditionFalse,
                    DisplayLabel = "If no",
                    SortOrder = 1,
                    NodeIds = [],
                },
            ],
            FlowContainerKind.Loop or FlowContainerKind.For or FlowContainerKind.ForEach or FlowContainerKind.While =>
            [
                new FlowLaneModel
                {
                    LaneId = $"lane-{Guid.NewGuid():N}",
                    ParentContainerNodeId = containerId,
                    LaneKind = FlowLaneKind.LoopBody,
                    DisplayLabel = "Loop Body",
                    SortOrder = 0,
                    NodeIds = [],
                },
            ],
            _ =>
            [
                new FlowLaneModel
                {
                    LaneId = $"lane-{Guid.NewGuid():N}",
                    ParentContainerNodeId = containerId,
                    LaneKind = FlowLaneKind.Sequential,
                    DisplayLabel = "Items",
                    SortOrder = 0,
                    NodeIds = [],
                },
            ],
        };
    }

    private static FlowContainerKind ResolveContainerKind(string actionId)
    {
        return actionId switch
        {
            "for-loop" => FlowContainerKind.For,
            "for-each-loop" => FlowContainerKind.ForEach,
            "while-loop" => FlowContainerKind.While,
            _ => FlowContainerKind.Group,
        };
    }

    private static ContainerParameters CreateDefaultContainerParameters(FlowContainerKind kind)
    {
        return kind switch
        {
            FlowContainerKind.For => new ForContainerParameters(),
            FlowContainerKind.ForEach => new ForEachContainerParameters(),
            FlowContainerKind.While => new WhileContainerParameters(),
            _ => new UnknownContainerParameters(),
        };
    }

    public FlowDocumentModel DeleteSelection(FlowDocumentModel document, IReadOnlyList<string> nodeIds, IReadOnlyList<string> edgeIds)
    {
        var targetNodeIds = new HashSet<string>(nodeIds, StringComparer.Ordinal);
        var targetEdgeIds = new HashSet<string>(edgeIds, StringComparer.Ordinal);

        var predecessorPairs = document.Edges
            .Where(edge => targetNodeIds.Contains(edge.ToNodeId) && !targetNodeIds.Contains(edge.FromNodeId))
            .Select(edge => edge.FromNodeId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var successorPairs = document.Edges
            .Where(edge => targetNodeIds.Contains(edge.FromNodeId) && !targetNodeIds.Contains(edge.ToNodeId))
            .Select(edge => edge.ToNodeId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var remainingNodes = document.Nodes.Where(node => !targetNodeIds.Contains(node.NodeId)).ToList();
        var remainingEdges = document.Edges
            .Where(edge => !targetEdgeIds.Contains(edge.EdgeId))
            .Where(edge => !targetNodeIds.Contains(edge.FromNodeId) && !targetNodeIds.Contains(edge.ToNodeId))
            .ToList();

        foreach (var fromNodeId in predecessorPairs)
        {
            foreach (var toNodeId in successorPairs)
            {
                if (fromNodeId == toNodeId)
                {
                    continue;
                }

                remainingEdges.Add(new FlowEdgeModel
                {
                    EdgeId = $"edge-{Guid.NewGuid():N}",
                    FromNodeId = fromNodeId,
                    ToNodeId = toNodeId,
                });
            }
        }

        var rootNodeIds = document.RootLane.NodeIds.Where(id => !targetNodeIds.Contains(id)).ToList();

        var rewrittenNodes = remainingNodes
            .Select(node => node is FlowContainerNodeModel container
                ? container with
                {
                    ChildLanes = container.ChildLanes
                        .Select(lane => lane with { NodeIds = lane.NodeIds.Where(id => !targetNodeIds.Contains(id)).ToList() })
                        .ToList(),
                }
                : node)
            .ToList();

        return document with
        {
            Nodes = LayoutContainers(rewrittenNodes),
            Edges = DeduplicateEdges(remainingEdges),
            RootLane = document.RootLane with { NodeIds = rootNodeIds },
            Selection = new FlowSelectionModel(),
        };
    }

    public FlowClipboardModel CopySelection(FlowDocumentModel document, IReadOnlyList<string> nodeIds, IReadOnlyList<string> edgeIds)
    {
        var selectedNodeIds = new HashSet<string>(nodeIds, StringComparer.Ordinal);
        var selectedEdgeIds = new HashSet<string>(edgeIds, StringComparer.Ordinal);

        var copiedNodes = document.Nodes.Where(node => selectedNodeIds.Contains(node.NodeId)).ToList();
        var copiedEdges = document.Edges.Where(edge => selectedEdgeIds.Contains(edge.EdgeId)).ToList();

        return new FlowClipboardModel
        {
            Nodes = copiedNodes,
            Edges = copiedEdges,
        };
    }

    public FlowDocumentModel PasteSelection(FlowDocumentModel document, FlowClipboardModel clipboard, double dx, double dy)
    {
        if (clipboard.Nodes.Count == 0)
        {
            return document;
        }

        var nodeIdMap = clipboard.Nodes.ToDictionary(node => node.NodeId, _ => $"node-{Guid.NewGuid():N}", StringComparer.Ordinal);

        var pastedNodes = clipboard.Nodes.Select(node =>
        {
            if (node is FlowActionNodeModel action)
            {
                return (FlowNodeModel)action with
                {
                    NodeId = nodeIdMap[action.NodeId],
                    Bounds = action.Bounds with { X = action.Bounds.X + dx, Y = action.Bounds.Y + dy },
                };
            }

            var container = (FlowContainerNodeModel)node;
            return (FlowNodeModel)(container with
            {
                NodeId = nodeIdMap[container.NodeId],
                Bounds = container.Bounds with { X = container.Bounds.X + dx, Y = container.Bounds.Y + dy },
                ChildLanes = container.ChildLanes.Select(lane => lane with { NodeIds = [] }).ToList(),
            });
        }).ToList();

        var pastedEdges = clipboard.Edges
            .Where(edge => nodeIdMap.ContainsKey(edge.FromNodeId) && nodeIdMap.ContainsKey(edge.ToNodeId))
            .Select(edge => edge with
            {
                EdgeId = $"edge-{Guid.NewGuid():N}",
                FromNodeId = nodeIdMap[edge.FromNodeId],
                ToNodeId = nodeIdMap[edge.ToNodeId],
            })
            .ToList();

        var rootNodeIds = document.RootLane.NodeIds.ToList();
        rootNodeIds.AddRange(pastedNodes.Select(node => node.NodeId));

        return document with
        {
            Nodes = LayoutContainers(document.Nodes.Concat(pastedNodes).ToList()),
            Edges = DeduplicateEdges(document.Edges.Concat(pastedEdges).ToList()),
            RootLane = document.RootLane with { NodeIds = rootNodeIds },
            Selection = new FlowSelectionModel { SelectedNodeIds = pastedNodes.Select(node => node.NodeId).ToList(), PrimaryNodeId = pastedNodes[0].NodeId },
        };
    }

    public FlowDocumentModel MoveNodesToLane(FlowDocumentModel document, IReadOnlyList<string> nodeIds, string laneId, int insertIndex)
    {
        var targetNodeIds = new HashSet<string>(nodeIds, StringComparer.Ordinal);
        if (targetNodeIds.Count == 0)
        {
            return document;
        }

        var targetLane = FindLane(document, laneId);
        if (!CanMoveNodesToLane(document, targetNodeIds, targetLane))
        {
            return document;
        }

        var orderedMoveIds = nodeIds.Where(targetNodeIds.Contains).Distinct(StringComparer.Ordinal).ToList();
        var updatedRoot = document.RootLane with { NodeIds = document.RootLane.NodeIds.Where(id => !targetNodeIds.Contains(id)).ToList() };
        var targetContainerId = string.Equals(targetLane.LaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal)
            ? null
            : targetLane.ParentContainerNodeId;

        var nodes = document.Nodes.Select(node =>
        {
            if (!targetNodeIds.Contains(node.NodeId))
            {
                return node;
            }

            return node with { ParentContainerNodeId = targetContainerId };
        }).ToList();

        if (string.Equals(targetLane.LaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            var nodeOrder = updatedRoot.NodeIds.ToList();
            insertIndex = Math.Clamp(insertIndex, 0, nodeOrder.Count);
            nodeOrder.InsertRange(insertIndex, orderedMoveIds);

            var updatedDocument = document with
            {
                Nodes = LayoutContainers(nodes),
                RootLane = updatedRoot with { NodeIds = nodeOrder },
            };

            return updatedDocument with { Edges = RebuildSequentialEdges(updatedDocument) };
        }

        var rewrittenNodes = nodes.Select(node =>
        {
            if (node is not FlowContainerNodeModel container)
            {
                return node;
            }

            var updatedLanes = container.ChildLanes.Select(lane =>
            {
                var current = lane.NodeIds.Where(id => !targetNodeIds.Contains(id)).ToList();
                if (!string.Equals(lane.LaneId, laneId, StringComparison.Ordinal))
                {
                    return lane with { NodeIds = current };
                }

                var index = Math.Clamp(insertIndex, 0, current.Count);
                current.InsertRange(index, orderedMoveIds);
                return lane with { NodeIds = current };
            }).ToList();

            return (FlowNodeModel)(container with { ChildLanes = updatedLanes });
        }).ToList();

        var next = document with
        {
            Nodes = LayoutContainers(rewrittenNodes),
            RootLane = updatedRoot,
        };

        return next with { Edges = RebuildSequentialEdges(next) };
    }

    private static bool CanMoveNodesToLane(FlowDocumentModel document, IReadOnlySet<string> movedNodeIds, FlowLaneModel targetLane)
    {
        if (string.Equals(targetLane.LaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            return true;
        }

        var targetContainerId = targetLane.ParentContainerNodeId;
        if (string.IsNullOrWhiteSpace(targetContainerId))
        {
            return true;
        }

        var containersById = document.Nodes
            .OfType<FlowContainerNodeModel>()
            .ToDictionary(container => container.NodeId, StringComparer.Ordinal);

        foreach (var movedNodeId in movedNodeIds)
        {
            if (string.Equals(movedNodeId, targetContainerId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!containersById.ContainsKey(movedNodeId))
            {
                continue;
            }

            if (IsDescendantContainer(containersById, targetContainerId, movedNodeId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDescendantContainer(
        IReadOnlyDictionary<string, FlowContainerNodeModel> containersById,
        string candidateContainerId,
        string possibleAncestorContainerId)
    {
        var currentId = candidateContainerId;

        while (!string.IsNullOrWhiteSpace(currentId))
        {
            if (string.Equals(currentId, possibleAncestorContainerId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!containersById.TryGetValue(currentId, out var container))
            {
                break;
            }

            currentId = container.ParentContainerNodeId;
        }

        return false;
    }

    public FlowDocumentModel TranslateNodes(FlowDocumentModel document, IReadOnlyList<string> nodeIds, double deltaX, double deltaY)
    {
        var selected = new HashSet<string>(nodeIds, StringComparer.Ordinal);
        if (selected.Count == 0)
        {
            return document;
        }

        var nodes = document.Nodes.Select(node =>
        {
            if (!selected.Contains(node.NodeId))
            {
                return node;
            }

            return node with
            {
                Bounds = node.Bounds with
                {
                    X = node.Bounds.X + deltaX,
                    Y = node.Bounds.Y + deltaY,
                },
            };
        }).ToList();

        return document with { Nodes = LayoutContainers(nodes) };
    }

    private static IReadOnlyList<FlowEdgeModel> DeduplicateEdges(IReadOnlyList<FlowEdgeModel> edges)
    {
        var unique = new Dictionary<string, FlowEdgeModel>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (string.IsNullOrWhiteSpace(edge.FromNodeId) || string.IsNullOrWhiteSpace(edge.ToNodeId))
            {
                continue;
            }

            var key = $"{edge.FromNodeId}>{edge.ToNodeId}>{edge.FromPort}>{edge.ToPort}";
            if (!unique.ContainsKey(key))
            {
                unique[key] = edge;
            }
        }

        return unique.Values.ToList();
    }

    private static IReadOnlyList<FlowEdgeModel> RebuildSequentialEdges(FlowDocumentModel document)
    {
        var preservedEdges = document.Edges
            .Where(edge => edge.FromPort != FlowPortKind.Output || edge.ToPort != FlowPortKind.Input)
            .ToList();

        AddLaneSequentialEdges(preservedEdges, document.RootLane, owningContainerNodeId: null);

        foreach (var container in document.Nodes.OfType<FlowContainerNodeModel>())
        {
            foreach (var lane in container.ChildLanes.OrderBy(lane => lane.SortOrder))
            {
                AddLaneSequentialEdges(preservedEdges, lane, container.NodeId);
            }
        }

        return DeduplicateEdges(preservedEdges);
    }

    private static void AddLaneSequentialEdges(List<FlowEdgeModel> edges, FlowLaneModel lane, string? owningContainerNodeId)
    {
        for (var index = 0; index < lane.NodeIds.Count - 1; index++)
        {
            var fromId = lane.NodeIds[index];
            var toId = lane.NodeIds[index + 1];
            if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
            {
                continue;
            }

            edges.Add(new FlowEdgeModel
            {
                EdgeId = $"edge-{Guid.NewGuid():N}",
                FromNodeId = fromId,
                ToNodeId = toId,
                FromPort = FlowPortKind.Output,
                ToPort = FlowPortKind.Input,
                LaneMetadata = string.Equals(lane.LaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal)
                    ? null
                    : new FlowEdgeLaneMetadataModel
                    {
                        SourceLaneId = lane.LaneId,
                        TargetLaneId = lane.LaneId,
                        OwningContainerNodeId = owningContainerNodeId,
                    },
            });
        }
    }

    private static IReadOnlyList<FlowNodeModel> LayoutContainers(IReadOnlyList<FlowNodeModel> nodes)
    {
        var lookup = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        return nodes.Select(node =>
        {
            if (node is not FlowContainerNodeModel container)
            {
                return node;
            }

            if (container.ChildLanes.Count == 0)
            {
                return node;
            }

            var maxWidth = container.Bounds.Width;
            var totalHeight = 62d;
            foreach (var lane in container.ChildLanes.OrderBy(l => l.SortOrder))
            {
                if (lane.NodeIds.Count == 0)
                {
                    totalHeight += 38;
                    continue;
                }

                var childBounds = lane.NodeIds
                    .Where(lookup.ContainsKey)
                    .Select(id => lookup[id].Bounds)
                    .ToList();

                if (childBounds.Count == 0)
                {
                    totalHeight += 38;
                    continue;
                }

                var laneMinY = childBounds.Min(bounds => bounds.Y);
                var laneMaxY = childBounds.Max(bounds => bounds.Y + bounds.Height);
                totalHeight += (laneMaxY - laneMinY) + 24;
                maxWidth = Math.Max(maxWidth, childBounds.Max(bounds => bounds.Width) + 36);
            }

            return (FlowNodeModel)(container with
            {
                Bounds = container.Bounds with
                {
                    Width = Math.Max(420, maxWidth),
                    Height = Math.Max(140, totalHeight),
                },
            });
        }).ToList();
    }
}

public sealed record FlowClipboardModel
{
    public IReadOnlyList<FlowNodeModel> Nodes { get; init; } = [];

    public IReadOnlyList<FlowEdgeModel> Edges { get; init; } = [];
}
