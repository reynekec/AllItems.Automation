using WpfAutomation.App.Models.Flow;
using WpfAutomation.App.NodeInspector.Contracts;
using WpfAutomation.App.NodeInspector.Models;
using WpfAutomation.App.Services.Flow;
using WpfAutomation.App.NodeInspector.ViewModels;

namespace WpfAutomation.App.NodeInspector.Services;

public sealed class DefaultNodeInspectorFactory : INodeInspectorFactory
{
    private readonly IFlowActionParameterResolver _parameterResolver;

    public DefaultNodeInspectorFactory()
        : this(new FlowActionParameterResolver())
    {
    }

    public DefaultNodeInspectorFactory(IFlowActionParameterResolver parameterResolver)
    {
        _parameterResolver = parameterResolver;
    }

    public INodeInspectorDescriptor CreateDescriptor(FlowActionNodeModel node)
    {
        return new NodeInspectorDescriptor
        {
            NodeId = node.NodeId,
            ActionId = node.ActionReference.ActionId,
            DisplayName = string.IsNullOrWhiteSpace(node.DisplayLabel) ? node.ActionReference.ActionId : node.DisplayLabel,
            CategoryName = node.ActionReference.CategoryName,
        };
    }

    public INodeInspectorViewModel CreateInspector(FlowActionNodeModel node, Action<ActionParameters> commit)
    {
        // When adding a new action inspector:
        // 1) Add its ActionParameters record in FlowActionParameters.cs
        // 2) Register default descriptor in FlowActionParameterResolver
        // 3) Add a dedicated view model + UserControl and map it below
        var actionId = node.ActionReference.ActionId;
        var descriptor = _parameterResolver.Resolve(actionId);
        var defaults = descriptor.DefaultValue;

        return actionId switch
        {
            "open-browser" => new OpenBrowserInspectorViewModel(Coerce<OpenBrowserActionParameters>(node.ActionParameters, defaults), (OpenBrowserActionParameters)defaults, commit),
            "new-page" => new NewPageInspectorViewModel(Coerce<NewPageActionParameters>(node.ActionParameters, defaults), (NewPageActionParameters)defaults, commit),
            "close-browser" => new CloseBrowserInspectorViewModel(Coerce<CloseBrowserActionParameters>(node.ActionParameters, defaults), (CloseBrowserActionParameters)defaults, commit),
            "navigate-to-url" => new NavigateToUrlInspectorViewModel(Coerce<NavigateToUrlActionParameters>(node.ActionParameters, defaults), (NavigateToUrlActionParameters)defaults, commit),
            "go-back" => new GoBackInspectorViewModel(Coerce<GoBackActionParameters>(node.ActionParameters, defaults), (GoBackActionParameters)defaults, commit),
            "go-forward" => new GoForwardInspectorViewModel(Coerce<GoForwardActionParameters>(node.ActionParameters, defaults), (GoForwardActionParameters)defaults, commit),
            "reload-page" => new ReloadPageInspectorViewModel(Coerce<ReloadPageActionParameters>(node.ActionParameters, defaults), (ReloadPageActionParameters)defaults, commit),
            "wait-for-url" => new WaitForUrlInspectorViewModel(Coerce<WaitForUrlActionParameters>(node.ActionParameters, defaults), (WaitForUrlActionParameters)defaults, commit),
            "click-element" => new ClickElementInspectorViewModel(Coerce<ClickElementActionParameters>(node.ActionParameters, defaults), (ClickElementActionParameters)defaults, commit),
            "fill-input" => new FillInputInspectorViewModel(Coerce<FillInputActionParameters>(node.ActionParameters, defaults), (FillInputActionParameters)defaults, commit),
            "hover-element" => new HoverElementInspectorViewModel(Coerce<HoverElementActionParameters>(node.ActionParameters, defaults), (HoverElementActionParameters)defaults, commit),
            "press-key" => new PressKeyInspectorViewModel(Coerce<PressKeyActionParameters>(node.ActionParameters, defaults), (PressKeyActionParameters)defaults, commit),
            "select-option" => new SelectOptionInspectorViewModel(Coerce<SelectOptionActionParameters>(node.ActionParameters, defaults), (SelectOptionActionParameters)defaults, commit),
            "expect-enabled" => new ExpectEnabledInspectorViewModel(Coerce<ExpectEnabledActionParameters>(node.ActionParameters, defaults), (ExpectEnabledActionParameters)defaults, commit),
            "expect-hidden" => new ExpectHiddenInspectorViewModel(Coerce<ExpectHiddenActionParameters>(node.ActionParameters, defaults), (ExpectHiddenActionParameters)defaults, commit),
            "expect-text" => new ExpectTextInspectorViewModel(Coerce<ExpectTextActionParameters>(node.ActionParameters, defaults), (ExpectTextActionParameters)defaults, commit),
            "expect-visible" => new ExpectVisibleInspectorViewModel(Coerce<ExpectVisibleActionParameters>(node.ActionParameters, defaults), (ExpectVisibleActionParameters)defaults, commit),
            _ => new UnknownActionInspectorViewModel(actionId, Coerce<UnknownActionParameters>(node.ActionParameters, defaults), (UnknownActionParameters)defaults, commit),
        };
    }

    public INodeInspectorDescriptor CreateContainerDescriptor(FlowContainerNodeModel node)
    {
        return new NodeInspectorDescriptor
        {
            NodeId = node.NodeId,
            ActionId = $"container:{node.ContainerKind.ToString().ToLowerInvariant()}",
            DisplayName = string.IsNullOrWhiteSpace(node.DisplayLabel) ? node.ContainerKind.ToString() : node.DisplayLabel,
            CategoryName = "Control Flow",
        };
    }

    public INodeInspectorViewModel CreateContainerInspector(FlowContainerNodeModel node, Action<ContainerParameters> commit)
    {
        return node.ContainerKind switch
        {
            FlowContainerKind.For => new ForContainerInspectorViewModel(CoerceContainer<ForContainerParameters>(node.ContainerParameters, new ForContainerParameters()), new ForContainerParameters(), commit),
            FlowContainerKind.ForEach => new ForEachContainerInspectorViewModel(CoerceContainer<ForEachContainerParameters>(node.ContainerParameters, new ForEachContainerParameters()), new ForEachContainerParameters(), commit),
            FlowContainerKind.While => new WhileContainerInspectorViewModel(CoerceContainer<WhileContainerParameters>(node.ContainerParameters, new WhileContainerParameters()), new WhileContainerParameters(), commit),
            _ => new UnknownContainerInspectorViewModel(node.ContainerKind.ToString(), CoerceContainer<UnknownContainerParameters>(node.ContainerParameters, new UnknownContainerParameters()), new UnknownContainerParameters(), commit),
        };
    }
    private static TParameters Coerce<TParameters>(ActionParameters current, ActionParameters defaultValue)
        where TParameters : ActionParameters
    {
        if (current is TParameters typed)
        {
            return typed;
        }

        return (TParameters)defaultValue;
    }

    private static TParameters CoerceContainer<TParameters>(ContainerParameters current, ContainerParameters defaultValue)
        where TParameters : ContainerParameters
    {
        if (current is TParameters typed)
        {
            return typed;
        }

        return (TParameters)defaultValue;
    }
}
