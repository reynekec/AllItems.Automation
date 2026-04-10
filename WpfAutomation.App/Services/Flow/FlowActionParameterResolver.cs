using WpfAutomation.App.Models.Flow;

namespace WpfAutomation.App.Services.Flow;

public sealed record FlowActionParameterDescriptor(Type ParameterType, ActionParameters DefaultValue);

public interface IFlowActionParameterResolver
{
    FlowActionParameterDescriptor Resolve(string actionId);
}

public sealed class FlowActionParameterResolver : IFlowActionParameterResolver
{
    private static readonly FlowActionParameterDescriptor UnknownDescriptor =
        new(typeof(UnknownActionParameters), new UnknownActionParameters());

    private static readonly IReadOnlyDictionary<string, FlowActionParameterDescriptor> Descriptors =
        new Dictionary<string, FlowActionParameterDescriptor>(StringComparer.Ordinal)
        {
            ["open-browser"] = new(typeof(OpenBrowserActionParameters), new OpenBrowserActionParameters()),
            ["new-page"] = new(typeof(NewPageActionParameters), new NewPageActionParameters()),
            ["close-browser"] = new(typeof(CloseBrowserActionParameters), new CloseBrowserActionParameters()),
            ["navigate-to-url"] = new(typeof(NavigateToUrlActionParameters), new NavigateToUrlActionParameters()),
            ["go-back"] = new(typeof(GoBackActionParameters), new GoBackActionParameters()),
            ["go-forward"] = new(typeof(GoForwardActionParameters), new GoForwardActionParameters()),
            ["reload-page"] = new(typeof(ReloadPageActionParameters), new ReloadPageActionParameters()),
            ["wait-for-url"] = new(typeof(WaitForUrlActionParameters), new WaitForUrlActionParameters()),
            ["wait-for-user-confirmation"] = new(typeof(WaitForUserConfirmationActionParameters), new WaitForUserConfirmationActionParameters()),
            ["click-element"] = new(typeof(ClickElementActionParameters), new ClickElementActionParameters()),
            ["fill-input"] = new(typeof(FillInputActionParameters), new FillInputActionParameters()),
            ["hover-element"] = new(typeof(HoverElementActionParameters), new HoverElementActionParameters()),
            ["press-key"] = new(typeof(PressKeyActionParameters), new PressKeyActionParameters()),
            ["select-option"] = new(typeof(SelectOptionActionParameters), new SelectOptionActionParameters()),
            ["expect-enabled"] = new(typeof(ExpectEnabledActionParameters), new ExpectEnabledActionParameters()),
            ["expect-hidden"] = new(typeof(ExpectHiddenActionParameters), new ExpectHiddenActionParameters()),
            ["expect-text"] = new(typeof(ExpectTextActionParameters), new ExpectTextActionParameters()),
            ["expect-visible"] = new(typeof(ExpectVisibleActionParameters), new ExpectVisibleActionParameters()),
        };

    public FlowActionParameterDescriptor Resolve(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return UnknownDescriptor;
        }

        return Descriptors.TryGetValue(actionId, out var descriptor)
            ? descriptor
            : UnknownDescriptor;
    }
}
