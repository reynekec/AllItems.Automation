using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.ControlFlow;

public sealed class ForLoopAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "for-loop",
        DisplayName: "For Loop",
        CategoryId: "control-flow",
        CategoryName: "Control Flow",
        IconKeyOrPath: "icon-control-flow-for",
        Keywords: ["for", "loop", "iteration", "counter"],
        SortOrder: 10,
        IsContainer: true);
}
