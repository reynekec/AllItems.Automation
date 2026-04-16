using AllItems.Automation.Browser.App.NodeInspector.Contracts;

namespace AllItems.Automation.Browser.App.NodeInspector.Models;

public enum NodeInspectorDisplayKind
{
    NoneSelected = 0,
    EdgeSelected = 1,
    ContainerSelected = 2,
    ActionInspector = 3,
    ActionInspectorUnavailable = 4,
}

public sealed record SelectedNodeInspectorState
{
    public NodeInspectorDisplayKind DisplayKind { get; init; } = NodeInspectorDisplayKind.NoneSelected;

    public string Title { get; init; } = "Node Inspector";

    public string Message { get; init; } = "Select a node to view editable properties.";

    public string? NodeId { get; init; }

    public INodeInspectorDescriptor? Descriptor { get; init; }

    public INodeInspectorViewModel? InspectorViewModel { get; init; }

    public bool HasInspector => InspectorViewModel is not null;

    public static SelectedNodeInspectorState CreateNone()
    {
        return new SelectedNodeInspectorState();
    }

    public static SelectedNodeInspectorState CreateEdgeSelected(string edgeId)
    {
        return new SelectedNodeInspectorState
        {
            DisplayKind = NodeInspectorDisplayKind.EdgeSelected,
            Title = "Edge Selected",
            Message = $"Edge '{edgeId}' is selected. Node inspector editing is available for action nodes.",
        };
    }

    public static SelectedNodeInspectorState CreateContainerSelected(string nodeId, string label)
    {
        return new SelectedNodeInspectorState
        {
            DisplayKind = NodeInspectorDisplayKind.ContainerSelected,
            NodeId = nodeId,
            Title = "Container Selected",
            Message = $"'{label}' is a container node. Container inspectors are planned but not yet available.",
        };
    }

    public static SelectedNodeInspectorState CreateActionUnavailable(string nodeId, INodeInspectorDescriptor descriptor)
    {
        return new SelectedNodeInspectorState
        {
            DisplayKind = NodeInspectorDisplayKind.ActionInspectorUnavailable,
            NodeId = nodeId,
            Descriptor = descriptor,
            Title = descriptor.DisplayName,
            Message = $"No inspector is registered for action '{descriptor.ActionId}'.",
        };
    }

    public static SelectedNodeInspectorState CreateActionInspector(string nodeId, INodeInspectorDescriptor descriptor, INodeInspectorViewModel viewModel)
    {
        return new SelectedNodeInspectorState
        {
            DisplayKind = NodeInspectorDisplayKind.ActionInspector,
            NodeId = nodeId,
            Descriptor = descriptor,
            InspectorViewModel = viewModel,
            Title = descriptor.DisplayName,
            Message = "Inspector loaded.",
        };
    }
}
