using System.IO;
using System.Text.Json;
using AllItems.Automation.Browser.App.Models.Flow;

namespace AllItems.Automation.Browser.App.Services.Flow;

public interface IFlowPersistenceService
{
    Task SaveAsync(FlowDocumentModel document, string filePath, CancellationToken cancellationToken = default);

    Task<FlowDocumentModel> OpenAsync(string filePath, CancellationToken cancellationToken = default);
}

public sealed class FlowPersistenceService : IFlowPersistenceService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public async Task SaveAsync(FlowDocumentModel document, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var snapshot = FlowSnapshotMapper.ToSnapshot(document);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, snapshot with { SchemaVersion = FlowDocumentSchema.CurrentVersion }, _jsonSerializerOptions, cancellationToken);
    }

    public async Task<FlowDocumentModel> OpenAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        await using var stream = File.OpenRead(filePath);
        var snapshot = await JsonSerializer.DeserializeAsync<FlowDocumentSnapshot>(stream, _jsonSerializerOptions, cancellationToken);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Flow file did not contain a valid document.");
        }

        if (snapshot.SchemaVersion > FlowDocumentSchema.CurrentVersion)
        {
            throw new InvalidOperationException($"Flow schema version {snapshot.SchemaVersion} is newer than supported version {FlowDocumentSchema.CurrentVersion}.");
        }

        return FlowSnapshotMapper.FromSnapshot(snapshot);
    }
}

public static class FlowSnapshotMapper
{
    private static readonly JsonSerializerOptions ParameterSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly IFlowActionParameterResolver ParameterResolver = new FlowActionParameterResolver();

    public static FlowDocumentSnapshot ToSnapshot(FlowDocumentModel document)
    {
        var nodeSnapshots = document.Nodes.Select(node =>
        {
            if (node is FlowActionNodeModel actionNode)
            {
                return new FlowNodeSnapshot
                {
                    NodeKind = FlowNodeKind.Action,
                    NodeId = actionNode.NodeId,
                    DisplayLabel = actionNode.DisplayLabel,
                    IsCollapsed = actionNode.IsCollapsed,
                    ParentContainerNodeId = actionNode.ParentContainerNodeId,
                    Bounds = new FlowNodeBoundsSnapshot
                    {
                        X = actionNode.Bounds.X,
                        Y = actionNode.Bounds.Y,
                        Width = actionNode.Bounds.Width,
                        Height = actionNode.Bounds.Height,
                    },
                    ActionReference = new FlowActionReferenceSnapshot
                    {
                        ActionId = actionNode.ActionReference.ActionId,
                        CategoryId = actionNode.ActionReference.CategoryId,
                        CategoryName = actionNode.ActionReference.CategoryName,
                        IconKeyOrPath = actionNode.ActionReference.IconKeyOrPath,
                        Keywords = actionNode.ActionReference.Keywords,
                    },
                    ActionParameters = new FlowActionParametersSnapshot
                    {
                        ParameterTypeName = actionNode.ActionParameters.GetType().Name,
                        JsonPayload = JsonSerializer.Serialize(actionNode.ActionParameters, actionNode.ActionParameters.GetType(), ParameterSerializerOptions),
                    },
                };
            }

            var containerNode = (FlowContainerNodeModel)node;
            return new FlowNodeSnapshot
            {
                NodeKind = FlowNodeKind.Container,
                NodeId = containerNode.NodeId,
                DisplayLabel = containerNode.DisplayLabel,
                IsCollapsed = containerNode.IsCollapsed,
                ParentContainerNodeId = containerNode.ParentContainerNodeId,
                ContainerKind = containerNode.ContainerKind,
                ContainerParameters = new FlowContainerParametersSnapshot
                {
                    ParameterTypeName = containerNode.ContainerParameters.GetType().Name,
                    JsonPayload = JsonSerializer.Serialize(containerNode.ContainerParameters, containerNode.ContainerParameters.GetType(), ParameterSerializerOptions),
                },
                IsExpanded = containerNode.IsExpanded,
                ChildLanes = containerNode.ChildLanes.Select(ToSnapshot).ToList(),
                Bounds = new FlowNodeBoundsSnapshot
                {
                    X = containerNode.Bounds.X,
                    Y = containerNode.Bounds.Y,
                    Width = containerNode.Bounds.Width,
                    Height = containerNode.Bounds.Height,
                },
            };
        }).ToList();

        return new FlowDocumentSnapshot
        {
            SchemaVersion = document.SchemaVersion,
            DocumentId = document.DocumentId,
            DisplayName = document.DisplayName,
            Viewport = new FlowViewportSnapshot
            {
                OffsetX = document.Viewport.OffsetX,
                OffsetY = document.Viewport.OffsetY,
                Zoom = document.Viewport.Zoom,
            },
            RootLane = ToSnapshot(document.RootLane),
            Nodes = nodeSnapshots,
            Edges = document.Edges.Select(edge => new FlowEdgeSnapshot
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                FromPort = edge.FromPort,
                ToPort = edge.ToPort,
                LaneMetadata = edge.LaneMetadata is null
                    ? null
                    : new FlowEdgeLaneMetadataSnapshot
                    {
                        SourceLaneId = edge.LaneMetadata.SourceLaneId,
                        TargetLaneId = edge.LaneMetadata.TargetLaneId,
                        OwningContainerNodeId = edge.LaneMetadata.OwningContainerNodeId,
                    },
            }).ToList(),
            Selection = new FlowSelectionSnapshot
            {
                PrimaryNodeId = document.Selection.PrimaryNodeId,
                PrimaryEdgeId = document.Selection.PrimaryEdgeId,
                SelectedNodeIds = document.Selection.SelectedNodeIds,
                SelectedEdgeIds = document.Selection.SelectedEdgeIds,
            },
        };
    }

    public static FlowDocumentModel FromSnapshot(FlowDocumentSnapshot snapshot)
    {
        var nodes = snapshot.Nodes.Select(node =>
        {
            if (node.NodeKind == FlowNodeKind.Action)
            {
                return (FlowNodeModel)new FlowActionNodeModel
                {
                    NodeId = node.NodeId,
                    DisplayLabel = node.DisplayLabel,
                    IsCollapsed = node.IsCollapsed,
                    ParentContainerNodeId = node.ParentContainerNodeId,
                    Bounds = new FlowNodeBounds
                    {
                        X = node.Bounds.X,
                        Y = node.Bounds.Y,
                        Width = node.Bounds.Width,
                        Height = node.Bounds.Height,
                    },
                    ActionReference = node.ActionReference is null
                        ? new FlowActionReferenceModel()
                        : new FlowActionReferenceModel
                        {
                            ActionId = node.ActionReference.ActionId,
                            CategoryId = node.ActionReference.CategoryId,
                            CategoryName = node.ActionReference.CategoryName,
                            IconKeyOrPath = node.ActionReference.IconKeyOrPath,
                            Keywords = node.ActionReference.Keywords,
                        },
                    ActionParameters = ResolveActionParameters(node.ActionReference?.ActionId, node.ActionParameters),
                };
            }

            var containerKind = node.ContainerKind ?? FlowContainerKind.Group;

            return (FlowNodeModel)new FlowContainerNodeModel
            {
                NodeId = node.NodeId,
                DisplayLabel = node.DisplayLabel,
                IsCollapsed = node.IsCollapsed,
                ParentContainerNodeId = node.ParentContainerNodeId,
                ContainerKind = containerKind,
                ContainerParameters = ResolveContainerParameters(containerKind, node.ContainerParameters),
                IsExpanded = node.IsExpanded ?? true,
                ChildLanes = node.ChildLanes.Select(FromSnapshot).ToList(),
                Bounds = new FlowNodeBounds
                {
                    X = node.Bounds.X,
                    Y = node.Bounds.Y,
                    Width = node.Bounds.Width,
                    Height = node.Bounds.Height,
                },
            };
        }).ToList();

        return new FlowDocumentModel
        {
            SchemaVersion = snapshot.SchemaVersion,
            DocumentId = snapshot.DocumentId,
            DisplayName = snapshot.DisplayName,
            Viewport = new FlowViewportModel
            {
                OffsetX = snapshot.Viewport.OffsetX,
                OffsetY = snapshot.Viewport.OffsetY,
                Zoom = snapshot.Viewport.Zoom,
            },
            RootLane = FromSnapshot(snapshot.RootLane),
            Nodes = nodes,
            Edges = snapshot.Edges.Select(edge => new FlowEdgeModel
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                FromPort = edge.FromPort,
                ToPort = edge.ToPort,
                LaneMetadata = edge.LaneMetadata is null
                    ? null
                    : new FlowEdgeLaneMetadataModel
                    {
                        SourceLaneId = edge.LaneMetadata.SourceLaneId,
                        TargetLaneId = edge.LaneMetadata.TargetLaneId,
                        OwningContainerNodeId = edge.LaneMetadata.OwningContainerNodeId,
                    },
            }).ToList(),
            Selection = new FlowSelectionModel
            {
                PrimaryNodeId = snapshot.Selection.PrimaryNodeId,
                PrimaryEdgeId = snapshot.Selection.PrimaryEdgeId,
                SelectedNodeIds = snapshot.Selection.SelectedNodeIds,
                SelectedEdgeIds = snapshot.Selection.SelectedEdgeIds,
            },
        };
    }

    private static FlowLaneSnapshot ToSnapshot(FlowLaneModel lane)
    {
        return new FlowLaneSnapshot
        {
            LaneId = lane.LaneId,
            LaneKind = lane.LaneKind,
            DisplayLabel = lane.DisplayLabel,
            SortOrder = lane.SortOrder,
            ParentContainerNodeId = lane.ParentContainerNodeId,
            NodeIds = lane.NodeIds,
        };
    }

    private static FlowLaneModel FromSnapshot(FlowLaneSnapshot lane)
    {
        return new FlowLaneModel
        {
            LaneId = lane.LaneId,
            LaneKind = lane.LaneKind,
            DisplayLabel = lane.DisplayLabel,
            SortOrder = lane.SortOrder,
            ParentContainerNodeId = lane.ParentContainerNodeId,
            NodeIds = lane.NodeIds,
        };
    }

    private static ActionParameters ResolveActionParameters(string? actionId, FlowActionParametersSnapshot? snapshot)
    {
        var descriptor = ParameterResolver.Resolve(actionId ?? string.Empty);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.JsonPayload))
        {
            return descriptor.DefaultValue;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize(snapshot.JsonPayload, descriptor.ParameterType, ParameterSerializerOptions);
            return parsed as ActionParameters ?? descriptor.DefaultValue;
        }
        catch
        {
            return descriptor.DefaultValue;
        }
    }

    private static ContainerParameters ResolveContainerParameters(FlowContainerKind kind, FlowContainerParametersSnapshot? snapshot)
    {
        ContainerParameters defaultParameters = kind switch
        {
            FlowContainerKind.For => new ForContainerParameters(),
            FlowContainerKind.ForEach => new ForEachContainerParameters(),
            FlowContainerKind.While => new WhileContainerParameters(),
            _ => new UnknownContainerParameters(),
        };

        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.JsonPayload))
        {
            return defaultParameters;
        }

        var targetType = defaultParameters.GetType();

        try
        {
            var parsed = JsonSerializer.Deserialize(snapshot.JsonPayload, targetType, ParameterSerializerOptions);
            return parsed as ContainerParameters ?? defaultParameters;
        }
        catch
        {
            return defaultParameters;
        }
    }
}
