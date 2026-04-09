using WpfAutomation.App.Models.Flow;

namespace WpfAutomation.App.NodeInspector.Contracts;

public interface INodeInspectorFactory
{
    INodeInspectorDescriptor CreateDescriptor(FlowActionNodeModel node);

    INodeInspectorViewModel CreateInspector(FlowActionNodeModel node, Action<ActionParameters> commit);

    INodeInspectorDescriptor CreateContainerDescriptor(FlowContainerNodeModel node);

    INodeInspectorViewModel CreateContainerInspector(FlowContainerNodeModel node, Action<ContainerParameters> commit);
}
