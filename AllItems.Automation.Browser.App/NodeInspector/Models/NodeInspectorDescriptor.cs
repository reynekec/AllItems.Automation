using AllItems.Automation.Browser.App.NodeInspector.Contracts;

namespace AllItems.Automation.Browser.App.NodeInspector.Models;

public sealed record NodeInspectorDescriptor : INodeInspectorDescriptor
{
    public string NodeId { get; init; } = string.Empty;

    public string ActionId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;
}
