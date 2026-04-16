using AllItems.Automation.Browser.App.Models.Flow;

namespace AllItems.Automation.Browser.App.NodeInspector.Contracts;

public interface INodeInspectorFactory
{
    INodeInspectorDescriptor CreateDescriptor(FlowActionNodeModel node);

    INodeInspectorViewModel CreateInspector(FlowActionNodeModel node, Action<ActionParameters> commit);

    INodeInspectorDescriptor CreateContainerDescriptor(FlowContainerNodeModel node);

    INodeInspectorViewModel CreateContainerInspector(FlowContainerNodeModel node, Action<ContainerParameters> commit);
}
