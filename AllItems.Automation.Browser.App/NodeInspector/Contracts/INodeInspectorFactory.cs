using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.NodeInspector.ViewModels;

namespace AllItems.Automation.Browser.App.NodeInspector.Contracts;

public interface INodeInspectorFactory
{
    INodeInspectorDescriptor CreateDescriptor(FlowActionNodeModel node);

    INodeInspectorViewModel CreateInspector(FlowActionNodeModel node, IReadOnlyList<ClickElementBrowserTargetOption> browserTargets, Action<ActionParameters> commit);

    INodeInspectorDescriptor CreateContainerDescriptor(FlowContainerNodeModel node);

    INodeInspectorViewModel CreateContainerInspector(FlowContainerNodeModel node, Action<ContainerParameters> commit);
}
